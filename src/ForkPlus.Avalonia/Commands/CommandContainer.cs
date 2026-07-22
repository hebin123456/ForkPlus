using System;
using System.Collections.Generic;

namespace ForkPlus.Avalonia.Commands
{
    // spike 命令基础设施：命令注册表，集中管理所有 IForkPlusCommand 实例。
    // 对照 WPF src/ForkPlus/UI/Commands/CommandContainer.cs（包含所有命令的实例化与按 Id 查找）。
    // spike 阶段提供 Register/Get/All 三个方法，供 MainWindowCommands 等调用方使用。
    public static class CommandContainer
    {
        private static readonly Dictionary<string, IForkPlusCommand> _commands = new();

        public static void Register(IForkPlusCommand command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            _commands[command.Id] = command;
        }

        public static IForkPlusCommand? Get(string id)
            => _commands.TryGetValue(id, out var c) ? c : null;

        public static IEnumerable<IForkPlusCommand> All => _commands.Values;
    }
}
