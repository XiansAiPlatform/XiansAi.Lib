/// <summary>
/// Reusable system-prompt fragment. The LLM reply is sent as-is — there is
/// no markdown converter. Copy this file into another agent and append
/// <see cref="Combine"/> to that agent's instructions.
/// </summary>
internal static class MarkdownFormattingPrompt
{
    public const string SharedRules =
        """
        Formatting (mandatory):
        - Never emit HTML. Slack strips tags; Teams text messages do not render them.
        - Do not rewrite formatting inside inline `code` or fenced code blocks.
        - There is no post-processor. The text you emit is delivered unchanged to Studio, Slack, or Teams. Follow the channel block below exactly.
        """;

    public const string Studio =
        """
        You are talking in Agent Studio. Write GitHub-flavored Markdown (GFM): **bold**, *italic*, ~~strikethrough~~, [text](url), ATX headings (`# Title`), lists, and fenced code. Never emit Slack mrkdwn.

        Users may paste GFM, Slack mrkdwn, or a mix. Realign reused content to clean GFM:
        - Slack *bold* → **bold**
        - Slack _italic_ → *italic*
        - Slack ~strike~ → ~~strike~~
        - Slack <https://example.com|label> → [label](https://example.com)
        - Broken or mixed markers → rewrite as valid GFM
        """;

    public const string Slack =
        """
        You are talking in Slack. Emit Slack mrkdwn in the reply itself. There is no converter.

        Slack mrkdwn (mandatory):
        - Bold: **bold** (double asterisks). Never *bold* — Slack treats a single *pair* as italic.
        - Italic: _italic_ (underscores). Never wrap a title or bold phrase in one *pair*.
        - Strikethrough: ~strike~. Never ~~strike~~.
        - Links: <https://example.com|label> or <https://example.com>. Never [text](url).
        - Images: convert ![alt](url) to <url|alt> (or <url> if alt is empty).
        - Headings: Slack has no heading levels. Do not use # Title, *Title*, or **Title** (those become italic). Put the title on its own line using Unicode mathematical sans-serif bold letters so it looks bold with no markdown markers. Put a blank line after each title.
          Mapping: A–Z → 𝗔–𝗭, a–z → 𝗮–𝘇, 0–9 → 𝟬–𝟵. Example: Heading 1 → 𝗛𝗲𝗮𝗱𝗶𝗻𝗴 𝟭
          Alphabet: 𝗔𝗕𝗖𝗗𝗘𝗙𝗚𝗛𝗜𝗝𝗞𝗟𝗠𝗡𝗢𝗣𝗤𝗥𝗦𝗧𝗨𝗩𝗪𝗫𝗬𝗭 𝗮𝗯𝗰𝗱𝗲𝗳𝗴𝗵𝗶𝗷𝗸𝗹𝗺𝗻𝗼𝗽𝗾𝗿𝘀𝘁𝘂𝘃𝘄𝘅𝘆𝘇 𝟬𝟭𝟮𝟯𝟰𝟱𝟲𝟳𝟴𝟵
        - Horizontal rules: a line of ────────
        - Task lists: ☑ / ☐ instead of [x] / [ ]
        - Numbered lists: Slack renders both `1. item` and `1) item` as bullets and drops the number. Write `(1) item` so the number stays. If a numbered list follows a bullet list, put a blank line between them.
        - Tables: write a GFM table with trailing pipes and a separator (`| Col A | Col B |` then `| --- | --- |`). Slack draws a native 2-column table from that. Never wrap a table in ```. Never use box-drawing.
        - Block quotes: do not use `>`. Write `▎ quote` so the line cannot be stripped as a blockquote.
        - Inline code: do not use backticks. Slack drops `code` and ``` fences. Write the code in Unicode mathematical monospace (𝚊–𝚣, 𝙰–𝚉, 𝟶–𝟿).
        - Fenced code: do not use ```. Prefix each line with `│ ` and write Unicode mathematical monospace. Do not draw ┌─┐ bars (they become rules and do not match the line width).

        If the user pasted GFM or mixed markers, convert that content to Slack mrkdwn before reusing it.
        """;

    public const string Teams =
        """
        You are talking in Microsoft Teams. Emit Teams text-message markdown in the reply itself. There is no converter.

        Teams text markdown (mandatory):
        - Bold, italic, and links stay GFM: **bold**, *italic*, [text](url).
        - Strikethrough: Teams text messages drop `~~strike~~` (tildes vanish, no line). Write combining long stroke overlay (U+0336) after every visible character. Skip whitespace. Example: strike → s̶t̶r̶i̶k̶e̶. Never leave raw tildes around the word.
        - Headings: Teams text messages do not render `# Title`. Write **Title** on its own line. Always put a blank line after each title so consecutive titles do not concatenate ("Heading 1Heading 2").
        - Images: convert ![alt](url) to [alt](url) (or the bare URL if alt is empty).
        - Horizontal rules: a line of ────────
        - Task lists: ☑ / ☐ instead of [x] / [ ]
        - Tables: write a GFM table with a leading pipe and no trailing pipe (`| Col A | Col B`, not `| Col A | Col B |`). A trailing pipe is parsed as an extra empty column. Never wrap a table in ```. Never use box-drawing ┃.
        - Never emit Slack mrkdwn (*bold*, _italic_, <url|text>).

        If the user pasted Slack mrkdwn or mixed markers, realign reused content to the Teams rules above.
        """;

    public static ChatChannel FromScope(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ChatChannel.Studio;
        }

        if (scope.Contains("Slack", StringComparison.OrdinalIgnoreCase))
        {
            return ChatChannel.Slack;
        }

        if (scope.Contains("Teams", StringComparison.OrdinalIgnoreCase)
            || scope.Contains("msteams", StringComparison.OrdinalIgnoreCase))
        {
            return ChatChannel.Teams;
        }

        return ChatChannel.Studio;
    }

    public static string For(ChatChannel channel)
    {
        var dialect = channel switch
        {
            ChatChannel.Slack => Slack,
            ChatChannel.Teams => Teams,
            _ => Studio
        };

        return $"{SharedRules}\n\n{dialect}";
    }

    public static string For(string? scope) => For(FromScope(scope));

    /// <summary>
    /// Prefix agent-specific personality / task instructions with the
    /// channel formatting block. Other agents should call this instead of
    /// inlining Slack/Teams rules.
    /// </summary>
    public static string Combine(string agentInstructions, ChatChannel channel) =>
        $"{agentInstructions.Trim()}\n\n{For(channel)}";

    public static string Combine(string agentInstructions, string? scope) =>
        Combine(agentInstructions, FromScope(scope));
}

internal enum ChatChannel
{
    Studio,
    Slack,
    Teams
}
