---
name: FrpNexus
colors:
  surface: '#fff8f2'
  surface-dim: '#e3d9ca'
  surface-bright: '#fff8f2'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#fdf2e3'
  surface-container: '#f7ecde'
  surface-container-high: '#f1e7d8'
  surface-container-highest: '#ece1d3'
  on-surface: '#201b12'
  on-surface-variant: '#4f4634'
  inverse-surface: '#353026'
  inverse-on-surface: '#faefe1'
  outline: '#817662'
  outline-variant: '#d3c5ae'
  surface-tint: '#795900'
  primary: '#795900'
  on-primary: '#ffffff'
  primary-container: '#d4a017'
  on-primary-container: '#503a00'
  inverse-primary: '#f6be39'
  secondary: '#5d5f5f'
  on-secondary: '#ffffff'
  secondary-container: '#dcdddd'
  on-secondary-container: '#5f6161'
  tertiary: '#185eaf'
  on-tertiary: '#ffffff'
  tertiary-container: '#72a9ff'
  on-tertiary-container: '#003d7a'
  error: '#ba1a1a'
  on-error: '#ffffff'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  primary-fixed: '#ffdfa0'
  primary-fixed-dim: '#f6be39'
  on-primary-fixed: '#261a00'
  on-primary-fixed-variant: '#5c4300'
  secondary-fixed: '#e2e2e2'
  secondary-fixed-dim: '#c6c6c7'
  on-secondary-fixed: '#1a1c1c'
  on-secondary-fixed-variant: '#454747'
  tertiary-fixed: '#d6e3ff'
  tertiary-fixed-dim: '#a9c7ff'
  on-tertiary-fixed: '#001b3d'
  on-tertiary-fixed-variant: '#00468b'
  background: '#fff8f2'
  on-background: '#201b12'
  surface-variant: '#ece1d3'
  terminal-bg: '#1C1C1C'
  surface-white: '#FFFFFF'
  border-low-contrast: '#E5E5E5'
  text-primary: rgba(0, 0, 0, 0.89)
  text-secondary: rgba(0, 0, 0, 0.62)
typography:
  title-lg:
    fontFamily: Segoe UI Variable, Microsoft YaHei
    fontSize: 28px
    fontWeight: '600'
    lineHeight: 36px
  body-md:
    fontFamily: Segoe UI Variable, Microsoft YaHei
    fontSize: 14px
    fontWeight: '400'
    lineHeight: 20px
  label-sm:
    fontFamily: Segoe UI Variable, Microsoft YaHei
    fontSize: 12px
    fontWeight: '500'
    lineHeight: 16px
  terminal-code:
    fontFamily: jetbrainsMono
    fontSize: 13px
    fontWeight: '400'
    lineHeight: 18px
rounded:
  sm: 0.25rem
  DEFAULT: 0.5rem
  md: 0.75rem
  lg: 1rem
  xl: 1.5rem
  full: 9999px
spacing:
  form-gutter: 16px
  section-padding: 24px
  terminal-inner: 12px
  margin-standard: 8px
---

# 🚀 FrpNexus (Arturia) - 跨平台 UI/UX 设计规范 (WinUI 3 / 浅色中文版)

> **[System Instruction for AI UI Generator]**
> 你是一个精通 **Windows 11 Fluent Design System** 和 **WinUI 3** 的高级前端/UI工程师。请严格按照 WinUI 3 的视觉语言和控件语义，生成名为 `FrpNexus` 的桌面端网络穿透管理工具的界面代码。
> 整体风格：**浅色模式 (Light Theme), 界面语言为中文, 严格遵循 WinUI 3 的云母材质、圆角、字距与细边框规范。**

## 一、 WinUI 3 设计令牌与材质 (Design Tokens & Materials)

请使用标准的 Fluent Design 变量，并融入 Arturia 品牌色：

### 1. 材质与背景 (Materials & Backgrounds)
*   **Window Background (云母材质)**: 整个应用的底层背景必须模拟 Windows 11 的 **Mica (云母)** 材质。若无法渲染真实云母，请使用回退纯色 `#F3F3F3`。
*   **Surface / Layer (控件图层)**: 右侧主工作区的内容卡片使用 `#FFFFFF`，并带有一圈极其微弱的系统级细边框 (`BorderBrush: #E5E5E5`, `BorderThickness: 1px`)，**不使用投影 (No Drop Shadows)**，WinUI 3 倾向于用边框来区分层级。
*   **Terminal Background**: 控制台区域模拟 Windows Terminal 风格，使用纯深色 `#1C1C1C`（深空灰）。

### 2. 品牌主题色 (Accent Color)
*   **SystemAccentColor (耀眼金)**: `#D4A017`。将标准 WinUI 的蓝色主题色替换为此金色。用于 `AccentButton`（主操作按钮）、ToggleSwitch 的开启状态、以及选中状态的指示条。
*   **TextFillColorPrimary**: `#E4000000` (WinUI 标准主文本色，即纯黑的 89% 透明度)。
*   **TextFillColorSecondary**: `#9E000000` (WinUI 标准次级文本色，即纯黑的 62% 透明度)。

### 3. 字体与图标 (Typography & Iconography)
*   **UI 字体**: 强制使用 `Segoe UI Variable`, `Microsoft YaHei`。
*   **终端/代码字体**: 强制使用 `Cascadia Code` 或 `Consolas`。
*   **图标系统**: 使用 `Segoe Fluent Icons` 风格的线框圆角图标。

### 4. 标准圆角 (Corner Radius)
*   **Overlay/Cards (大容器)**: `CornerRadius="8"`。
*   **Buttons/TextBox (交互控件)**: `CornerRadius="4"`。

---

## 二、 核心布局架构 (WinUI 3 Layout Structure)

请使用标准的 WinUI 3 控件语义结构进行布局：

### 1. 左侧导航栏 (NavigationView) 
*   **控件语义**: 使用 `NavigationView` (Left 模式)。
*   **顶部 Header**: 一个标准的 App Title Bar，包含极简的盾牌/剑型 Logo 图标 and 文本 "FrpNexus"。
*   **MenuItems**: 
    *   图标 (Icon) + 文本 (如：`本地 Web 服务`, `SSH 远程桌面`)。
    *   选中状态 (Selected)：左侧显示系统默认的 `AccentColor`（耀眼金）短竖线，项背景变为浅灰色。
*   **FooterMenuItems**: 包含 "设置 (Settings)"，使用标准齿轮图标。

### 2. 右侧工作区 (Content Area) 
由上下两个区域构成（上方明亮 Fluent UI，下方深色 Windows Terminal）：

#### 上半区：配置面板 (Page Content)
*   **Page Header**: 
    *   左侧大标题：**本地 Web 服务** (`TextBlock Style="TitleTextBlockStyle"`, 字号 28px)。
    *   右侧状态：一个 WinUI 3 的 `InfoBadge`（信息徽章）或圆点图标，显示 "已连接" (`AccentColor`) 或 "离线" (`TextFillColorSecondary`)。
*   **配置表单区 (Form)**:
    *   使用标准的 WinUI 3 `TextBox` 控件。带有顶部 Label。默认状态下仅底部有边框线，Focus 时四周呈现 `AccentColor` 边框。
    *   间距：表单项之间保持 `16px` 的标准 Fluent 间距。
*   **操作区 (Action)**:
    *   使用标准的 WinUI `Button`，配置 `Style="AccentButtonStyle"`（背景为耀眼金，文字为纯白或深灰以保证对比度）。
    *   文本：**"启动穿透"**。

#### 下半区：终端守护者控制台 (Terminal Container)
*   模拟内嵌的 **Windows Terminal** 界面。
*   **容器样式**: 带有 `CornerRadius="8"` 的纯黑/深灰圆角框，四周有 `1px` 的深色边框以融合进浅色背景。
*   **内边距**: `12px`。
*   **内容**: 等宽字体滚动的中文日志。
    `[14:05:22 INFO] AvalonDaemon 守护进程已就绪...`
    `[14:05:24 SUCCESS] 穿透隧道建立成功。`

---

## 三、 微交互规范 (Fluent Motion & Feedback)

严格按照 Windows 11 的物理反馈进行交互：
1.  **Reveal Highlight (光照反馈)**: 当鼠标悬停在左侧导航项或表单背景上时，呈现极微妙的光照渐变（若 AI 无法生成光照，则使用基础的底色加深模拟 Hover）。
2.  **Pointer Down (按压反馈)**: 当点击“启动穿透”按钮时，按钮必须有轻微的缩放动画 (`transform: scale(0.97)`，持续时间极其短促)。
3.  **State Change**: 点击启动后，按钮文本变为 "停止穿透"，同时移除 `AccentButtonStyle`，变回普通的灰色 `StandardButton`，避免在运行期间持续抢夺视觉焦点。
