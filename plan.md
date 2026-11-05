# DesmosSharp 开发计划

## 1. 目的与约束

1. 目标：原生复刻 https://www.desmos.com/calculator 的核心体验（图形计算器）。
2. 技术栈：Avalonia 12.1.5 + Material.Avalonia 3.21.0，.NET 10。
3. 平台：先 Windows；Linux/macOS 后续验证。
4. AOT：当前不启用，但代码保持 AOT 友好（源生成序列化、避免反射型 MVVM），为将来留路。
5. 参考资料（均位于 `ref/`）：
   - `ref/Desmos-Offline-Mode-develop/`：他人的 Web 版离线复刻（DSOM）。
   - `ref/desmos-online-graphing-calculator-user-manual.pdf`：官方用户手册（功能范围依据）。

## 2. 参考资料分析结论

`Desmos-Offline-Mode` **不是复刻实现，而是 Web 外壳**：它下载 Desmos 官方 JS 资源（`preload_desmos.js`、`calculator_api.js`），加载后直接修改 `window.Desmos` 的内部私有对象来注入顶栏、侧边栏、快捷键等。

因此：

- 其代码**不可移植**到 Avalonia 原生项目（它没有解析器、没有渲染器，全部依赖 Desmos 黑盒）。
- 可复用的只有三样：
  1. 功能清单与交互细节（PDF + 其 UI 结构）；
  2. 图谱状态 JSON 的字段形状（`version / graph.viewport / graph.settings / expressions.list[]`），可用于设计兼容的存档格式；
  3. 资源版本化 / 回滚的工程思路（本项目不下载 Desmos 资源，不采用）。

**本项目本质**：从零实现表达式引擎 + 曲线求解器 + 图形渲染器 + 数学输入 UI，Avalonia 只承担壳与画布。

## 3. 技术选型（版本已在 NuGet 核实）

| 层面 | 选择 | 说明 |
|---|---|---|
| 运行时 | .NET 10（SDK 10.0.101） | `TargetFramework=net10.0`；Avalonia 12.1.5 提供 net10.0 程序集 |
| UI 框架 | Avalonia 12.1.5 | 默认启用编译绑定，XAML 必须写 `x:DataType` |
| 主题 | Material.Avalonia 3.21.0 | 唯一主题，**不引入 FluentTheme**；依赖 Avalonia >= 12.1.1，兼容 |
| 绘图 | SkiaSharp 3.119.4 + `ICustomDrawOperation` 直绘 | 必须锁定 3.119.4，与 Avalonia.Skia 保持一致，否则 `SKCanvas` 类型冲突 |
| MVVM | CommunityToolkit.Mvvm 8.4.0 | 源生成器，AOT 友好 |
| 序列化 | System.Text.Json + source generator | AOT 前提 |
| 多语言 | 内置 `LocalizationService` + JSON 语言包 | 见 5.5；不依赖 resx 卫星程序集 |
| 测试 | xUnit + Avalonia.Headless.XUnit 12.1.5 | 数学内核 golden 数值、渲染金图对比 |
| 包管理 | 中心包管理 `Directory.Packages.props` | 统一锁定版本 |

注意：
- `Avalonia.Diagnostics` 已无 12.x 版本（停在 11.3.22），DevTools 引法按官方文档确认。
- 移除 FluentTheme 后所有控件模板完全由 Material.Avalonia 提供；若出现某个控件无样式，优先补 `Material.Avalonia` 的子包，而不是回退 Fluent。

## 4. 解决方案结构

```
DesmosSharp.sln
Directory.Build.props                 # 统一 TFM / LangVersion / Nullable / 分析器
Directory.Packages.props              # 中心包管理
src/
  DesmosSharp.Core/                   # 零 UI 依赖：词法 / 语法 / AST / 求值 / 函数库
    Parsing/  Evaluation/  Functions/  Analysis/
  DesmosSharp.Graphing/               # 视口、刻度、采样、隐函数求解、特殊点
  DesmosSharp.Rendering/              # SkiaSharp 渲染层（依赖 Avalonia + Graphing）
  DesmosSharp.App/                    # Avalonia 壳：Views / ViewModels / Controls / Services / Assets
tests/
  DesmosSharp.Core.Tests/             # 解析器、求值器
  DesmosSharp.Graphing.Tests/         # 采样与求解器
  DesmosSharp.App.HeadlessTests/      # UI 冒烟 + 金图
```

硬约束：`DesmosSharp.Core` 不得引用 Avalonia，保证内核可单测、可复用。

## 5. 核心模块设计

### 5.1 解析与求值

优先级（低 → 高）：

```
分段 / 列表
关系 = < <= > >=
+ -
* / 及隐式乘法（左结合，1/2x = x/2）
一元 - +
^（右结合，且高于一元负号：-2^2 = -4，2^3^2 = 512）
后缀 ! / 下标 x_1
原子：数字、函数调用、|x|、点 (a,b)、列表 [1,2,3]
```

- 类型系统：`Real / Boolean / Point / List / Undefined`，实现列表广播语义。
- 求值：AST 一次编译为 delegate 树；建立**表达式依赖图**做增量更新，拖动滑块只重算依赖项。
- 表达式分类器（决定绘制方式）：
  - 含 `x,y` 的等式 → 隐函数
  - 含 `r,θ` → 极坐标
  - 坐标为 `(a,b)` 且含 `t` → 参数方程
  - 含 `<>` → 区域
  - 单个自由变量的等式 → 显函数 / 竖直线

### 5.2 曲线求解与渲染

- 显函数：按屏幕像素列自适应采样（递归细分），检测跳变与发散后断开线段，避免渐近线连成竖线。
- 隐函数 `F(x,y)=0`：marching squares（2~3px 网格 + 边二分细化），视口变化异步重算并缓存。
- 不等式：粗网格符号采样填充，边界复用等值线；严格不等式边界虚线。
- 刻度：1/2/5×10^k 取整 + π 标签模式，按像素密度自适应。
- 性能预算：20 条表达式下平移缩放 60fps；单点求值 < 50ns；隐函数单帧 < 4ms。

### 5.3 UI 与交互

- 表达式行：序号 + 颜色块 + 输入框 + 隐藏勾选 + 删除/复制/转表格菜单；拖动排序；行内错误提示。
- 滑块：自动识别未定义自由变量并提示创建；min/max/step 双击编辑；播放/暂停 + 速度。
- 撤销/重做：命令栈，Ctrl+Z / Ctrl+Y。
- 快捷键：Enter 新建行、方向键移动、Ctrl+S / Ctrl+Shift+S、Ctrl+O。
- 数学输入分阶段：v1 为「文本输入 + 排版预览」，2D 结构化编辑器作为后续独立项。

### 5.4 持久化

自研 `.dsm.json`，字段贴近 Desmos state 子集，便于导入参考项目导出的文件：

```json
{
  "version": 1,
  "title": "",
  "graph": {
    "viewport": { "xmin": -10, "xmax": 10, "ymin": -10, "ymax": 10 },
    "settings": { "polar": false, "degreeMode": false, "showGrid": true, "labels": true, "projector": false }
  },
  "expressions": {
    "list": [
      { "id": "1", "type": "expression", "latex": "y=mx+b", "color": "#c74440", "hidden": false }
    ]
  }
}
```

### 5.5 多语言

- 语言包为 JSON，存放于 `src/DesmosSharp.App/Assets/i18n/<code>.json`，以 `AvaloniaResource` 嵌入；新增语言 = 加一个 JSON 文件 + 在 `LocalizationService.SupportedLanguages` 注册一项。
- `LocalizationService` 单例，提供索引器 `this[key]`、`SelectedLanguage` 与 `Languages`；切换语言时更新 `CultureInfo.CurrentUICulture` 并触发 `Item[]` 变更，界面上所有本地化绑定即时刷新，无需重启。
- XAML 用法：`Text="{i18n:Localize Expressions}"`，由 `LocalizeExtension` 生成到服务索引器的绑定。
- 缺失键的回退链：当前语言 → `en` → 键名本身，保证漏翻不会显示空白。
- 支持语言：`zh-Hans`、`en`；后续按需追加 `ja`、`de` 等。
- JSON 读取使用 System.Text.Json 源生成上下文，保持 AOT 友好。

### 5.6 输入语法兼容（对照 `docs/输入.md`）

已支持：

| 写法 | 说明 |
|---|---|
| `x^2`、`sqrt`、`cbrt`、`pi`/`π`、`abs`/`\|x\|` | 快捷拼写与符号 |
| `sin(x)`、`f(x)=cos(x)`、`ln(x)`、`log(x)` | 标准函数与用户函数 |
| `log_2 x`、`sin x`、`sinx` | 下标记法与并置实参（函数只吃下一个幂级操作数） |
| `y=ax+b` | 连续字母拆成单字母变量（`a*x+b`），P4 据此生成滑块 |
| `y1`、`x2` | 尾随数字自动转为下标 `y_1`、`x_2` |
| `>=`、`<=`、`y=2x{x>0}`、`y={x<0: -x, x>=0: x}` | 不等式、定义域限制、分段函数 |

待实现：

- `~` 回归拟合：随 P6 数据表一起做。
- 虚拟键盘与数学排版（分式上下结构、根号模板）：随 P3 数学输入编辑器。
- `Σ`、`Π`、`d/dx`、`∫`：随 P5 高级图形。

已知取舍：并置实参按「幂级」绑定，因此 `sin x^2` = `sin(x^2)`、`sin x * 2` = `sin(x)*2`；Desmos 因为输入时自动插入括号模板，`sin2x` 会视作 `sin(2x)`。P3 的结构化编辑器落地后再对齐。

## 6. 分阶段路线图

| 阶段 | 内容 | 验收标准 | 粗估 |
|---|---|---|---|
| **P0 骨架** | sln、项目分层、Material 主题、布局占位、CI | `dotnet build/test` 全绿，窗口正常显示 | 1d |
| **P1 数学内核** | 词法 / AST / Pratt 解析 / 求值 / 内置函数（手册第 9 页全表）/ 分类器 | 200+ 单测通过；数值误差 < 1e-12；四类图形正确分类 | 5-8d |
| **P2 渲染基础** | Skia 画布、网格/坐标轴/刻度、滚轮缩放（光标锚定）、拖拽平移、显函数曲线 | 缩放跟手不掉帧；`y=sin(x)`、`y=1/x` 断点不连线 | 4-6d |
| **P3 表达式列表** | 行模型、文本输入 + 即时解析、错误提示、删除/复制/排序、颜色、隐藏、撤销重做 | 输入即出图，常见表达式零崩溃 | 6-10d |
| **P4 参数与滑块** | 自由变量识别、滑块创建/编辑/拖动、播放动画 | `y=mx+b` 自动生成两滑块；动画流畅 | 3-5d |
| **P5 高级图形** | 点/点阵/列表、参数方程、分段、隐函数、不等式、极坐标 | 手册第 7 页 10 种图形全部可画 | 8-12d |
| **P6 数据表** | 表格增删行列、单元格编辑、`x_1` 引用、表达式转表格 | 手输数据出散点图，列可被表达式引用 | 4-6d |
| **P7 设置与输出** | 极坐标/角度弧度/π 标签/投影仪模式、保存加载、PNG/SVG 导出、快捷键、多语言补全（中/英，可扩展） | 状态可往返序列化；导出图像正确；切换语言即时生效 | 5-7d |
| **P8 打磨发布** | 性能优化、异常兜底、Windows 单文件打包 | 20 条表达式流畅；`dotnet publish` 可分发 | 5-8d |

合计约 40-60 人日（单人 2-3 个月）；P0-P4 构成可用 MVP（约 1 个月）。

**当前进度**：
- P0 已完成——解决方案与项目分层、中心包管理、Material 主题窗口、表达式面板与画板占位、xUnit v3 + Avalonia.Headless 测试链路、Windows CI 工作流均已就位。
- P1 核心已完成——词法分析、AST、优先级解析器（隐式乘法 / `-2^2=-4` / `2^3^2=512` / 绝对值 / 点 / 列表 / 分段 / 定义域限制 / 下标）、值类型与列表广播、表达式编译执行、用户函数、内置函数库、表达式分类器。当前 78 个测试全绿。
- P1 待补——求和 `Σ`、连乘 `Π`、数值导数 `d/dx` 需要 AST 级变量绑定与区间语义，随 P5 高级图形一起做；统计回归不属于目标范围。
- 主题与多语言——已移除 FluentTheme，仅保留 Material.Avalonia；已接入 JSON 语言包与运行时切换（zh-Hans / en），界面文案全部走本地化绑定。
- 输入语法——按 `docs/输入.md` 对齐：连续字母拆分（`ax`→`a*x`）、尾随数字下标（`y1`→`y_1`）、函数并置实参（`sin x`、`log_2 x`）。
- P2 渲染基础已完成——`GraphViewport` 缩放（光标锚定）/平移/等比单位、`TickGenerator`（1/2/5×10^k 与 π 标签）、`FunctionSampler`（像素列采样 + 中点判定断开渐近线）、Skia 直绘的 `GraphDrawOperation`（网格/坐标轴/刻度文字/曲线）。
- P3 表达式列表已完成——行内即时解析与红色错误提示、颜色轮换、显示/隐藏、复制、上移/下移、删除、清空、Enter 新建并聚焦；快照式撤销/重做（连续输入合并为一步）。当前 120 个测试全绿。
- P3 未做——拖拽排序、LaTeX 式数学排版（分式/根号可视化），留待数学编辑器专项。
- P4 参数与滑块已完成——自由变量自动生成滑块（同名参数跨表达式共享）、滑块与数值框双向联动并即时重算曲线、`⋯` 按钮或双击滑块展开 min/max/step 编辑、播放/暂停动画（33ms 定时器、到达边界后往返）。当前 131 个测试全绿。
- P5 高级图形已完成——绘图模型由「单一显函数」升级为 `PlotItem`（显函数、竖直线、参数方程、极坐标、点、点列、隐函数、不等式），`PlotGeometryBuilder` 统一产出折线 / 散点 / 线段 / 填充矩形；隐函数用 marching squares（4px 网格 + 鞍点中心判定），不等式按扫描线合并单元格填充并复用等值线画边界；分段与定义域限制沿用求值期的 Undefined 语义自动断开。参数识别排除 `x/y/t/theta/r`。当前 155 个测试全绿。
- P5 未做——严格不等式的虚线边界、网格/映射结果缓存（留待 P8 性能优化）、`Σ`/`Π`/`d/dx`/`∫`（需要 AST 级变量绑定）。

## 7. 风险与对策

| 风险 | 等级 | 对策 |
|---|---|---|
| 二维数学输入编辑器 | 高 | 分阶段：v1 文本输入 + 排版预览；结构化编辑器单独立项，不阻塞主线 |
| 隐函数 / 不等式性能 | 高 | 网格降采样 + 边二分细化 + 异步计算 + 结果缓存 |
| 解析歧义（隐式乘法、`\|x\|`、下标、`=` 多义） | 中 | 建立 Desmos 行为语料作为回归测试集 |
| 功能蔓延 | 高 | 明确非目标清单，每个里程碑冻结范围 |
| Avalonia 与 SkiaSharp 版本冲突 | 中 | 中心包管理锁定 SkiaSharp 3.119.4 |
| 名称 / 图标 / 配色商标问题 | 中 | 不使用 Desmos 名称与 Logo，配色自定义 |

## 8. 非目标

账号体系、云端保存、分享链接与嵌入代码、3D 计算器、几何工具、统计回归、Desmos JS API 兼容、移动端、AOT 打包、Linux/macOS 验证（P8 之后评估）。
