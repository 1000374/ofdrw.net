# .NET Framework 4.0 (net40) 兼容性指南

Ofdrw.Net 为核心 OFD 基础库提供了 .NET Framework 4.0（`net40`）的目标框架支持，使得老旧系统、工业控制软件及传统 .NET Framework 客户端环境能够原生解析、生成、操作与签署 OFD 文档。

---

## 1. 目标框架与模块范围

### 支持 net40 的基础模块

以下 7 个基础模块同时面向 `net40`、`netstandard2.0` 与 `netstandard2.1` 编译并分发：

| 模块名 | 功能说明 | 依赖说明 (net40) |
| :--- | :--- | :--- |
| `Ofdrw.Net.Core` | OFD 核心模型、常量、基础接口与内部兼容 polyfill | `Microsoft.Bcl.Async` |
| `Ofdrw.Net.Packaging` | OFD ZIP 容器读写、资源映射与结构校验 | `Microsoft.Bcl.Async`, `DotNetZip` (仅 net40) |
| `Ofdrw.Net.Reader` | OFD 文档解析、元素树提取与文本提取 | `Ofdrw.Net.Core`, `Ofdrw.Net.Packaging` |
| `Ofdrw.Net.Layout` | OFD 文档合并与多文档流式页面组织 | `Ofdrw.Net.Core`, `Ofdrw.Net.Packaging`, `Ofdrw.Net.Reader` |
| `Ofdrw.Net.Converter.Abstractions` | 转换器抽象契约与通用选项 | `Ofdrw.Net.Core` |
| `Ofdrw.Net.Converter.Svg` | OFD 页面矢量导出为 SVG | `Ofdrw.Net.Core`, `Ofdrw.Net.Reader` |
| `Ofdrw.Net.Signatures` | SM3/SHA-256 哈希、包完整性校验与签名结构 | `Ofdrw.Net.Core`, `Ofdrw.Net.Packaging` |

### 明确不支持 net40 的高级/工具模块

以下模块依赖现代图形排版库（如 PDFsharp/ImageSharp/SixLabors.Fonts）、OpenXML 或系统外部进程调用，仅面向 `netstandard2.0` / `netstandard2.1` 及 `net10.0`：

- `Ofdrw.Net.Converter.Pdf`
- `Ofdrw.Net.Converter.Docx`
- `Ofdrw.Net.Converter`
- `Ofdrw.Net.Cli`

---

## 2. 运行时与编译要求

### 消费方运行环境要求
- **CLR 版本**: .NET Framework 4.0 或更高版本（CLR 4.0.30319）。
- **Windows 版本**: Windows XP SP3, Windows 7 SP1, Windows Server 2003/2008 及后续版本。
- **异步支持**: net40 目标框架下依赖 `Microsoft.Bcl.Async` (v1.0.168) 提供 `async/await` 支持。

### 编译环境要求
- 仓库源码支持 SDK-style 项目跨框架编译。
- 使用 `Microsoft.NETFramework.ReferenceAssemblies.net40` (v1.0.3) 提供框架参考程序集，在 Linux、macOS 或 Windows 构建机上均无需安装 .NET 4.0 完整 SDK 即可完成跨目标编译。

---

## 3. ZIP 容器实现与安全性保证

.NET Framework 4.0 基础类库中缺少 `System.IO.Compression.ZipArchive`（该类型于 .NET Framework 4.5 及 .NET Standard 引入）。针对此差异，Ofdrw.Net 采取了严格的条件编译与安全隔离策略：

1. **框架隔离**:
   - 在 `netstandard2.0` / `netstandard2.1` 下，完全维持原有基于标准 `System.IO.Compression.ZipArchive` 的实现，无任何第三方 ZIP 依赖。
   - 在 `net40` 下，采用经过验证的 `DotNetZip` (v1.16.0，采用商业友好的 BSD-3-Clause 许可证) 独立完成 ZIP 包的读取与写出。
2. **防 Zip Slip（路径穿越）保护**:
   - 在读取 ZIP 条目时，强制校验规范化路径，对包含 `../`、`..\` 或绝对路径根目录的恶意条目主动抛出 `InvalidDataException`，杜绝文件系统逃逸风险。
3. **流式与非 Seekable 支持**:
   - `OfdPackageReader` 与 `OfdPackageWriter` 支持从不可寻址流（如网络流、PipeStream）读取并解压至内存结构，写出时保证条目流完整刷新。

---

## 4. 内部 Polyfill 与语言特性兼容

为了在支持 C# 现代语法的同时完全兼容 .NET Framework 4.0，Ofdrw.Net 内部实现了严谨的向下兼容适配：

- **只读集合接口**:
  .NET 4.0 缺失 `IReadOnlyCollection<T>`, `IReadOnlyList<T>`, `IReadOnlyDictionary<TKey, TValue>`。在 `net40` 下由 `Ofdrw.Net.Core` 内部提供对应的类型定义与适配器，对外公共方法签名与泛型行为完全对齐。
- **Task 与异步辅助**:
  提供 `TaskCompat` 工具类，在 `net40` 下桥接至 `Microsoft.Bcl.Async` 的 `TaskEx.FromResult`, `TaskEx.Delay`, `TaskEx.WhenAll`, `TaskEx.WhenAny`，在现代运行时下直接内联至原生 `Task`。
- **ValueTuple 规避**:
  内部数据传递避免引用 `System.ValueTuple` 引起的程序集依赖冲突，采用轻量级只读 `struct` 及 `Deconstruct` 解构方法。

---

## 5. 验证套件

仓库包含专门面向 .NET Framework 4.0 的独立验证项目 `tests/Ofdrw.Net.Net40.SmokeTests`，在真实 CLR 4.0 环境下执行并断言以下 10 大核心场景：

1. **程序集目标框架断言**: 运行时反射断言 7 个基础模块的 `TargetFrameworkAttribute` 均为 `.NETFramework,Version=v4.0`。
2. **文档创建与打包写入**: 验证元数据、页面物理尺寸、多图层与 ZIP 归档输出。
3. **重读与文本提取**: 验证写出的 OFD 文档完整重新加载并正确提取 UTF-8 文本。
4. **页面动态操作**: 验证页面增、删、改、重新排序及页码重算。
5. **SVG 矢量导出**: 验证 `OfdToSvgConverter` 在 net40 下正确将 OFD 页面转换为符合规范的 SVG 标签。
6. **SM3 & SHA-256 密码学测试向量**: 严格对照 GB/T 32918 国家标准与 NIST 测试向量验证摘要算法在 net40 下的逐字节一致性。
7. **数字签名与结构校验**: 验证 OFD 签名的创建、哈希清单提取与参考校验。
8. **不可寻址流读取**: 验证非 Seekable 流正常加载。
9. **目录穿越 (Zip Slip) 防御**: 验证包含 `../` 的恶意包被安全拦截拒绝。
10. **包结构校验**: 验证 `OfdPackageStructureChecker` 诊断能力。
