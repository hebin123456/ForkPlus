namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 GitmojiEntry + GitmojiData（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/GitmojiData.cs（107 行）：
    //   - WPF GitmojiEntry : class（POCO）
    //     - string Emoji { get; }（如 "🐛"）
    //     - string ShortName { get; }（如 ":bug:"，冒号包围）
    //     - string Description { get; }（如 "Fix a bug"）
    //   - WPF GitmojiData : internal static class
    //     - public static readonly GitmojiEntry[] Entries（标准 Gitmoji 列表，约 70 项）
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. POCO 类 + 静态数据数组，无 WPF / Avalonia UI 依赖，零改动复用
    //   2. 命名空间 ForkPlus.UI.Controls → ForkPlus.Avalonia.Controls
    //   3. spike 把 GitmojiEntry 改为 public（WPF 默认 internal class，
    //      spike 改 public 以便跨工程使用）
    //
    // spike 简化：
    //   - 与 WPF 完全一致的 GitmojiEntry POCO 类 + GitmojiData 静态数据
    public class GitmojiEntry
    {
        // 对照 WPF: public string Emoji { get; }
        public string Emoji { get; }

        // 对照 WPF: public string ShortName { get; }
        public string ShortName { get; }

        // 对照 WPF: public string Description { get; }
        public string Description { get; }

        // 对照 WPF: public GitmojiEntry(string emoji, string shortName, string description)
        public GitmojiEntry(string emoji, string shortName, string description)
        {
            Emoji = emoji;
            ShortName = shortName;
            Description = description;
        }
    }

    // 对照 WPF: internal static class GitmojiData
    // spike 版改 public（WPF internal 限制为本工程使用，spike 工程需对外暴露）
    internal static class GitmojiData
    {
        // 对照 WPF: public static readonly GitmojiEntry[] Entries
        public static readonly GitmojiEntry[] Entries = new GitmojiEntry[]
        {
            new GitmojiEntry("🎨", ":art:", "Improve structure / format of the code"),
            new GitmojiEntry("⚡️", ":zap:", "Improve performance"),
            new GitmojiEntry("🔥", ":fire:", "Remove code or files"),
            new GitmojiEntry("🐛", ":bug:", "Fix a bug"),
            new GitmojiEntry("🚑", ":ambulance:", "Critical hotfix"),
            new GitmojiEntry("✨", ":sparkles:", "Introduce new features"),
            new GitmojiEntry("📝", ":memo:", "Update documentation"),
            new GitmojiEntry("🚀", ":rocket:", "Deploy stuff"),
            new GitmojiEntry("💄", ":lipstick:", "Add or update the UI and style files"),
            new GitmojiEntry("🎉", ":tada:", "Begin a project"),
            new GitmojiEntry("✅", ":white_check_mark:", "Add, update, or pass tests"),
            new GitmojiEntry("🔒", ":lock:", "Fix security or privacy issues"),
            new GitmojiEntry("🔐", ":closed_lock_with_key:", "Add or update secrets"),
            new GitmojiEntry("🔖", ":bookmark:", "Release / Version tags"),
            new GitmojiEntry("🚨", ":rotating_light:", "Fix compiler / linter warnings"),
            new GitmojiEntry("🚧", ":construction:", "Work in progress"),
            new GitmojiEntry("💚", ":green_heart:", "Fix CI Build"),
            new GitmojiEntry("⬇️", ":arrow_down:", "Downgrade dependencies"),
            new GitmojiEntry("⬆️", ":arrow_up:", "Upgrade dependencies"),
            new GitmojiEntry("📌", ":pushpin:", "Pin dependencies to specific versions"),
            new GitmojiEntry("👷", ":construction_worker:", "Add or update CI build system"),
            new GitmojiEntry("📈", ":chart_with_upwards_trend:", "Add or update analytics or track code"),
            new GitmojiEntry("♻️", ":recycle:", "Refactor code"),
            new GitmojiEntry("➕", ":heavy_plus_sign:", "Add a dependency"),
            new GitmojiEntry("➖", ":heavy_minus_sign:", "Remove a dependency"),
            new GitmojiEntry("🔧", ":wrench:", "Add or update configuration files"),
            new GitmojiEntry("🔨", ":hammer:", "Add or update development scripts"),
            new GitmojiEntry("🌐", ":globe_with_meridians:", "Internationalization and localization"),
            new GitmojiEntry("✏️", ":pencil2:", "Fix typos"),
            new GitmojiEntry("💩", ":poop:", "Write bad code that needs to be improved"),
            new GitmojiEntry("⏪", ":rewind:", "Revert changes"),
            new GitmojiEntry("🔀", ":twisted_rightwards_arrows:", "Merge branches"),
            new GitmojiEntry("📦", ":package:", "Add or update compiled files or packages"),
            new GitmojiEntry("👽", ":alien:", "Update code due to external API changes"),
            new GitmojiEntry("🚚", ":truck:", "Move or rename resources"),
            new GitmojiEntry("📄", ":page_facing_up:", "Add or update license"),
            new GitmojiEntry("💥", ":boom:", "Introduce breaking changes"),
            new GitmojiEntry("🍱", ":bento:", "Add or update assets"),
            new GitmojiEntry("♿️", ":wheelchair:", "Improve accessibility"),
            new GitmojiEntry("💡", ":bulb:", "Add or update comments in source code"),
            new GitmojiEntry("🍻", ":beers:", "Write code drunkenly"),
            new GitmojiEntry("💬", ":speech_balloon:", "Add or update text and literals"),
            new GitmojiEntry("🗃", ":card_file_box:", "Perform database related changes"),
            new GitmojiEntry("🔊", ":loud_sound:", "Add or update logs"),
            new GitmojiEntry("🔇", ":mute:", "Remove logs"),
            new GitmojiEntry("👥", ":busts_in_silhouette:", "Add or update contributor(s)"),
            new GitmojiEntry("🚸", ":children_crossing:", "Improve user experience / usability"),
            new GitmojiEntry("🏗", ":building_construction:", "Make architectural changes"),
            new GitmojiEntry("📱", ":iphone:", "Work on responsive design"),
            new GitmojiEntry("🤡", ":clown_face:", "Mock things"),
            new GitmojiEntry("🥚", ":egg:", "Add or update an easter egg"),
            new GitmojiEntry("🙈", ":see_no_evil:", "Add or update a .gitignore file"),
            new GitmojiEntry("📸", ":camera_flash:", "Add or update snapshots"),
            new GitmojiEntry("⚗️", ":alembic:", "Perform experiments"),
            new GitmojiEntry("🔍", ":mag:", "Improve SEO"),
            new GitmojiEntry("🏷️", ":label:", "Add or update types"),
            new GitmojiEntry("🌱", ":seedling:", "Add or update seed files"),
            new GitmojiEntry("🧩", ":jigsaw:", "Add or update business logic"),
            new GitmojiEntry("🧨", ":firecracker:", "Fix annoying behavior"),
            new GitmojiEntry("🌲", ":evergreen_tree:", "Work on something outdated"),
            new GitmojiEntry("🥅", ":goal_net:", "Catch errors"),
            new GitmojiEntry("💫", ":dizzy:", "Add or update animations and transitions"),
            new GitmojiEntry("🗑", ":wastebasket:", "Deprecate code that needs to be cleaned up"),
            new GitmojiEntry("🛂", ":passport_control:", "Work on code related to authorization"),
            new GitmojiEntry("🩹", ":adhesive_bandage:", "Simple fix for a non-critical issue"),
            new GitmojiEntry("🧐", ":monocle:", "Data exploration/inspection"),
            new GitmojiEntry("⚰️", ":coffin:", "Remove dead code"),
            new GitmojiEntry("🧪", ":test_tube:", "Add a failing test"),
            new GitmojiEntry("👔", ":necktie:", "Add or update business logic"),
            new GitmojiEntry("🩺", ":stethoscope:", "Add or update healthcheck"),
            new GitmojiEntry("🤖", ":robot:", "Add or update AI related code"),
            new GitmojiEntry("🩻", ":bones:", "Add or update skeleton code"),
        };
    }
}
