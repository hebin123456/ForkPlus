using Xunit;

// 禁用本 assembly 内的测试并行执行。
// ForkPlus ST 启动真实 WPF 进程 + 共享 %LOCALAPPDATA%\ForkPlus\settings.json，
// 并发会触发文件锁（IOException "being used by another process"）和进程冲突。
// 串行执行确保每个测试独占 settings.json 和 UIA 会话。
[assembly: CollectionBehavior(DisableTestParallelization = true)]
