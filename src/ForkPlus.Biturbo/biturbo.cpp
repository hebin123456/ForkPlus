#include <windows.h>

#include <algorithm>
#include <atomic>
#include <cctype>
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <fstream>
#include <map>
#include <memory>
#include <mutex>
#include <queue>
#include <sstream>
#include <string>
#include <thread>
#include <unordered_map>
#include <unordered_set>
#include <vector>

#include "md4c/md4c-html.h"

extern "C" {

enum BtResult : int32_t {
    Ok = 0,
    Err = 1,
    ErrCanceled = 2,
    ErrNotFound = 3,
};

struct BtOid { uint32_t s0, s1, s2, s3, s4; };
struct BtOidPair { BtOid left, right; };
struct BtRange { uint32_t start, end; };
struct BtRect { double x, y, w, h; };
struct BtPatchToken { uint8_t kind; uint32_t start; uint32_t end; };
struct BtParsePatchResult { void* tokens; int64_t tokens_len; int64_t tokens_cap; };
struct BtMdToHtmlResult { char* html; };
struct BtDecodeImageResult { uint8_t* data; int64_t data_len; int64_t data_cap; };
struct BtBehindAheadCount { uint32_t left, right; };
struct BtBehindAheadCounts { void* items; int64_t items_len; int64_t items_cap; };
struct BtCommitterTimes { void* times; int64_t times_len; int64_t times_cap; };
struct BtCommitStorage { void* oids; int64_t oids_len; int64_t oids_cap; void* indexes; int64_t indexes_len; int64_t indexes_cap; uint8_t has_more; };
struct BtCommitGraphCache { void* inner; };
struct BtCancellationToken { void* inner; };
struct BtProcessCancellationToken { void* inner; };
struct BtGitConfig { void* sections; int64_t sections_len; int64_t sections_cap; };
struct BtGitConfigVariable { char* name; char* value; };
struct BtGitConfigSection { char* name; char* sub_section; void* variables; int64_t variables_len; int64_t variables_cap; };
struct BtRepositoryManager { void* source_dirs; int64_t source_dirs_len; int64_t source_dirs_cap; uint8_t scan_depth; void* ignore; int64_t ignore_len; int64_t ignore_cap; void* repositories; int64_t repositories_len; int64_t repositories_cap; };
struct BtRepositoryManagerRepository { char* path; char* alias; uint32_t opened; uint8_t color; };
struct BtTagDetails { BtOid tag_object_oid; char* tagger_name; char* tagger_email; int64_t tagger_time; char* name; char* message; };
struct BtReferences { void* names_data; int64_t names_data_len; int64_t names_data_cap; void* names_offsets; int64_t names_offsets_len; int64_t names_offsets_cap; void* oids; int64_t oids_len; int64_t oids_cap; void* symrefs_data; int64_t symrefs_data_len; int64_t symrefs_data_cap; void* symrefs_offsets; int64_t symrefs_offsets_len; int64_t symrefs_offsets_cap; uint64_t hash; };
struct BtTree { void* entries; int64_t entries_len; int64_t entries_cap; };
struct BtTreeItem { uint16_t kind; char* filename; BtOid treeish; };
struct BtHead { BtOid DetachedHead; char* Reference; };
struct BtIdentity { char* name; char* email; };
struct BtRevisionHeader { int64_t author_index; int64_t author_time; char* subject; uint8_t has_body; };
struct BtRevisionHeaders { void* revisions; int64_t revisions_len; int64_t revisions_cap; void* identities; int64_t identities_len; int64_t identities_cap; };
struct BtStash { int32_t reflog_id; BtOid oid; BtOid first_parent; int64_t author_index; int64_t author_time; char* subject; };
struct BtRepositoryStashes { void* stashes; int64_t stashes_len; int64_t stashes_cap; void* identities; int64_t identities_len; int64_t identities_cap; };
struct BtSearchCommitsResult { void* matches; int64_t matches_len; int64_t matches_cap; };
struct BtHighlighedRange { BtRange range_utf16; uint8_t style; };
struct BtHighlightedDiff { void* items; int64_t items_len; int64_t items_cap; };
struct BtTreemapItem { int64_t index; BtRect rect; };
struct BtLayoutTreemapResult { void* items; int64_t items_len; int64_t items_cap; };
struct BtSpawnWithOutputResult { int32_t status; uint8_t* stdout_data; int64_t stdout_len; int64_t stdout_cap; uint8_t* stderr_data; int64_t stderr_len; int64_t stderr_cap; };
struct BtSpawnWithCallbackResult { int32_t status; };
typedef void(__cdecl* ReadLineCallback)(void* cb_target_ptr, uint8_t kind, uint8_t* data_ptr, int64_t data_len);

}

namespace {
thread_local std::string g_last_error;

struct BtOidHash {
    size_t operator()(const BtOid& oid) const noexcept {
        uint64_t hash = 1469598103934665603ull;
        auto mix = [&](uint32_t value) {
            hash ^= value;
            hash *= 1099511628211ull;
        };
        mix(oid.s0);
        mix(oid.s1);
        mix(oid.s2);
        mix(oid.s3);
        mix(oid.s4);
        return static_cast<size_t>(hash);
    }
};

struct BtOidEqual {
    bool operator()(const BtOid& a, const BtOid& b) const noexcept {
        return a.s0 == b.s0 && a.s1 == b.s1 && a.s2 == b.s2 && a.s3 == b.s3 && a.s4 == b.s4;
    }
};

BtResult run_process(char* path, char* current_dir, char** args, int64_t args_len, uint8_t* stdin_ptr, int64_t stdin_len, std::string& out_stdout, std::string& out_stderr, int& status);
std::string trim(const std::string& s);
bool starts_with(const std::string& value, const char* prefix);
BtOid oid_from_raw20(const uint8_t* raw);
struct GitObject;
bool read_git_object(char* git_dir, const BtOid& oid, GitObject& object, BtCommitGraphCache* cache_handle = nullptr);

void set_error(const std::string& message) {
    g_last_error = message;
}

char* dup_string(const std::string& s) {
    char* result = static_cast<char*>(std::malloc(s.size() + 1));
    if (!result) return nullptr;
    std::memcpy(result, s.data(), s.size());
    result[s.size()] = 0;
    return result;
}

template <typename T>
T* dup_array(const std::vector<T>& values) {
    if (values.empty()) return nullptr;
    T* result = static_cast<T*>(std::malloc(sizeof(T) * values.size()));
    if (!result) return nullptr;
    std::memcpy(result, values.data(), sizeof(T) * values.size());
    return result;
}

void release_ptr(void*& ptr) {
    if (ptr) {
        std::free(ptr);
        ptr = nullptr;
    }
}

void release_char(char*& ptr) {
    if (ptr) {
        std::free(ptr);
        ptr = nullptr;
    }
}

std::wstring utf8_to_wide(const char* text) {
    if (!text) return L"";
    int len = MultiByteToWideChar(CP_UTF8, 0, text, -1, nullptr, 0);
    if (len <= 0) return L"";
    std::wstring out(static_cast<size_t>(len - 1), L'\0');
    MultiByteToWideChar(CP_UTF8, 0, text, -1, out.data(), len);
    return out;
}

std::string wide_to_utf8(const std::wstring& text) {
    if (text.empty()) return "";
    int len = WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), nullptr, 0, nullptr, nullptr);
    std::string out(static_cast<size_t>(len), '\0');
    WideCharToMultiByte(CP_UTF8, 0, text.data(), static_cast<int>(text.size()), out.data(), len, nullptr, nullptr);
    return out;
}

std::wstring quote_arg(const std::wstring& arg) {
    if (arg.find_first_of(L" \t\n\v\"") == std::wstring::npos) return arg;
    std::wstring result = L"\"";
    size_t slash_count = 0;
    for (wchar_t ch : arg) {
        if (ch == L'\\') {
            slash_count++;
        } else if (ch == L'"') {
            result.append(slash_count * 2 + 1, L'\\');
            result.push_back(ch);
            slash_count = 0;
        } else {
            result.append(slash_count, L'\\');
            slash_count = 0;
            result.push_back(ch);
        }
    }
    result.append(slash_count * 2, L'\\');
    result.push_back(L'"');
    return result;
}

std::wstring build_command(char* path, char** args, int64_t args_len) {
    std::wstring command = quote_arg(utf8_to_wide(path));
    for (int64_t i = 0; i < args_len; ++i) {
        command.push_back(L' ');
        command += quote_arg(utf8_to_wide(args[i]));
    }
    return command;
}

std::string html_escape(const std::string& s) {
    std::string out;
    out.reserve(s.size());
    for (char ch : s) {
        switch (ch) {
        case '&': out += "&amp;"; break;
        case '<': out += "&lt;"; break;
        case '>': out += "&gt;"; break;
        case '"': out += "&quot;"; break;
        default: out.push_back(ch); break;
        }
    }
    return out;
}

std::string inline_markdown(const std::string& s) {
    std::string out;
    for (size_t i = 0; i < s.size();) {
        if (i + 1 < s.size() && s[i] == '!' && s[i + 1] == '[') {
            size_t alt_end = s.find("](", i + 2);
            size_t url_end = alt_end == std::string::npos ? std::string::npos : s.find(')', alt_end + 2);
            if (alt_end != std::string::npos && url_end != std::string::npos) {
                out += "<img src=\"" + html_escape(s.substr(alt_end + 2, url_end - alt_end - 2)) + "\" alt=\"" + html_escape(s.substr(i + 2, alt_end - i - 2)) + "\" />";
                i = url_end + 1;
                continue;
            }
        }
        if (s[i] == '[') {
            size_t text_end = s.find("](", i + 1);
            size_t url_end = text_end == std::string::npos ? std::string::npos : s.find(')', text_end + 2);
            if (text_end != std::string::npos && url_end != std::string::npos) {
                out += "<a href=\"" + html_escape(s.substr(text_end + 2, url_end - text_end - 2)) + "\">" + html_escape(s.substr(i + 1, text_end - i - 1)) + "</a>";
                i = url_end + 1;
                continue;
            }
        }
        if (i + 1 < s.size() && s[i] == '*' && s[i + 1] == '*') {
            size_t end = s.find("**", i + 2);
            if (end != std::string::npos) {
                out += "<strong>" + html_escape(s.substr(i + 2, end - i - 2)) + "</strong>";
                i = end + 2;
                continue;
            }
        }
        if (i + 1 < s.size() && s[i] == '~' && s[i + 1] == '~') {
            size_t end = s.find("~~", i + 2);
            if (end != std::string::npos) {
                out += "<del>" + html_escape(s.substr(i + 2, end - i - 2)) + "</del>";
                i = end + 2;
                continue;
            }
        }
        if (s[i] == '\\' && i + 1 < s.size()) {
            out += html_escape(std::string(1, s[i + 1]));
            i += 2;
            continue;
        }
        if (s[i] == '`') {
            size_t end = s.find('`', i + 1);
            if (end != std::string::npos) {
                out += "<code>" + html_escape(s.substr(i + 1, end - i - 1)) + "</code>";
                i = end + 1;
                continue;
            }
        }
        out += html_escape(std::string(1, s[i]));
        i++;
    }
    return out;
}

std::vector<std::string> split_markdown_table_row(const std::string& line) {
    std::string value = trim(line);
    if (!value.empty() && value.front() == '|') value.erase(value.begin());
    if (!value.empty() && value.back() == '|') value.pop_back();
    std::vector<std::string> cells;
    std::string current;
    bool escape = false;
    for (char ch : value) {
        if (escape) {
            current.push_back(ch);
            escape = false;
        } else if (ch == '\\') {
            escape = true;
        } else if (ch == '|') {
            cells.push_back(trim(current));
            current.clear();
        } else {
            current.push_back(ch);
        }
    }
    cells.push_back(trim(current));
    return cells;
}

bool is_markdown_table_separator(const std::string& line) {
    std::vector<std::string> cells = split_markdown_table_row(line);
    if (cells.empty()) return false;
    for (const std::string& cell : cells) {
        std::string value = trim(cell);
        if (value.empty()) return false;
        size_t start = value.front() == ':' ? 1 : 0;
        size_t end = value.size();
        if (end > start && value[end - 1] == ':') --end;
        if (end <= start) return false;
        for (size_t i = start; i < end; ++i) {
            if (value[i] != '-') return false;
        }
    }
    return true;
}

std::string unquote(const std::string& s) {
    std::string value = trim(s);
    if (value.size() >= 2 && value.front() == '"' && value.back() == '"') {
        value = value.substr(1, value.size() - 2);
    }
    return value;
}

std::string quote_toml(const std::string& s) {
    std::string out = "\"";
    for (char ch : s) {
        if (ch == '\\' || ch == '"') out.push_back('\\');
        out.push_back(ch);
    }
    out.push_back('"');
    return out;
}

std::vector<std::string> parse_string_array(const std::string& value) {
    std::vector<std::string> result;
    size_t start = value.find('[');
    size_t end = value.rfind(']');
    if (start == std::string::npos || end == std::string::npos || end <= start) return result;
    std::string body = value.substr(start + 1, end - start - 1);
    bool in_string = false;
    bool escape = false;
    std::string current;
    for (char ch : body) {
        if (escape) {
            current.push_back(ch);
            escape = false;
        } else if (ch == '\\') {
            escape = true;
        } else if (ch == '"') {
            if (in_string) {
                result.push_back(current);
                current.clear();
            }
            in_string = !in_string;
        } else if (in_string) {
            current.push_back(ch);
        }
    }
    return result;
}

std::string format_string_array(char** values, int64_t len) {
    std::ostringstream output;
    output << "[";
    for (int64_t i = 0; i < len; ++i) {
        if (i > 0) output << ", ";
        output << quote_toml(values[i] ? values[i] : "");
    }
    output << "]";
    return output.str();
}

std::string simple_markdown_to_html(const char* md) {
    std::istringstream input(md ? md : "");
    std::ostringstream html;
    std::string line;
    bool in_pre = false;
    bool in_list = false;
    bool in_ordered_list = false;
    bool in_blockquote = false;
    bool in_table = false;
    bool table_header_pending = false;
    std::string pre_language;
    std::string table_paragraph;
    auto flush_table = [&]() {
        if (in_table) {
            html << "</tbody></table>\n";
            in_table = false;
            table_header_pending = false;
        } else if (!table_paragraph.empty()) {
            html << "<p>" << html_escape(table_paragraph) << "</p>\n";
            table_paragraph.clear();
        }
    };
    while (std::getline(input, line)) {
        if (line.rfind("```", 0) == 0) {
            flush_table();
            if (in_list) {
                html << "</ul>\n";
                in_list = false;
            }
            if (in_ordered_list) {
                html << "</ol>\n";
                in_ordered_list = false;
            }
            if (in_blockquote) {
                html << "</blockquote>\n";
                in_blockquote = false;
            }
            if (in_pre) {
                html << "</code></pre>\n";
            } else {
                pre_language = trim(line.substr(3));
                html << "<pre><code";
                if (!pre_language.empty()) {
                    html << " class=\"language-" << html_escape(pre_language) << "\"";
                }
                html << ">";
            }
            in_pre = !in_pre;
            continue;
        }
        if (in_pre) {
            html << html_escape(line) << "\n";
            continue;
        }
        if (starts_with(line, "|")) {
            std::streampos old_pos = input.tellg();
            std::string next_line;
            if (!in_table && std::getline(input, next_line)) {
                if (!next_line.empty() && next_line.back() == '\r') next_line.pop_back();
                if (is_markdown_table_separator(next_line)) {
                    std::vector<std::string> headers = split_markdown_table_row(line);
                    html << "<table><thead><tr>";
                    for (const std::string& cell : headers) html << "<th>" << inline_markdown(cell) << "</th>";
                    html << "</tr></thead><tbody>\n";
                    in_table = true;
                    table_header_pending = false;
                    continue;
                }
                if (old_pos != std::streampos(-1)) {
                    input.seekg(old_pos);
                } else {
                    table_paragraph += line;
                    table_paragraph += "\n";
                    table_paragraph += next_line;
                    continue;
                }
            }
            if (in_table) {
                std::vector<std::string> cells = split_markdown_table_row(line);
                html << "<tr>";
                for (const std::string& cell : cells) html << "<td>" << inline_markdown(cell) << "</td>";
                html << "</tr>\n";
            } else {
                if (!table_paragraph.empty()) table_paragraph += "\n";
                table_paragraph += line;
            }
            continue;
        }
        flush_table();
        if (starts_with(line, "- ") || starts_with(line, "* ")) {
            if (in_ordered_list) {
                html << "</ol>\n";
                in_ordered_list = false;
            }
            if (in_blockquote) {
                html << "</blockquote>\n";
                in_blockquote = false;
            }
            if (!in_list) {
                html << "<ul>\n";
                in_list = true;
            }
            std::string item = line.substr(2);
            if (starts_with(item, "[ ] ") || starts_with(item, "[x] ") || starts_with(item, "[X] ")) {
                bool is_checked = item.size() > 1 && (item[1] == 'x' || item[1] == 'X');
                html << "<li><input type=\"checkbox\" disabled";
                if (is_checked) html << " checked";
                html << " /> " << inline_markdown(item.substr(4)) << "</li>\n";
            } else {
                html << "<li>" << inline_markdown(item) << "</li>\n";
            }
            continue;
        }
        if ((starts_with(line, "  - ") || starts_with(line, "    - ")) && in_list) {
            std::string item = trim(line);
            item = item.size() > 2 ? item.substr(2) : item;
            html << "<li><ul><li>" << inline_markdown(item) << "</li></ul></li>\n";
            continue;
        }
        size_t ordered_dot = line.find(". ");
        bool ordered = ordered_dot != std::string::npos && ordered_dot > 0 && std::all_of(line.begin(), line.begin() + ordered_dot, [](unsigned char ch) { return std::isdigit(ch); });
        if (ordered) {
            if (in_list) {
                html << "</ul>\n";
                in_list = false;
            }
            if (in_blockquote) {
                html << "</blockquote>\n";
                in_blockquote = false;
            }
            if (!in_ordered_list) {
                html << "<ol>\n";
                in_ordered_list = true;
            }
            html << "<li>" << inline_markdown(line.substr(ordered_dot + 2)) << "</li>\n";
            continue;
        }
        if (starts_with(line, "> ")) {
            if (in_list) {
                html << "</ul>\n";
                in_list = false;
            }
            if (in_ordered_list) {
                html << "</ol>\n";
                in_ordered_list = false;
            }
            if (!in_blockquote) {
                html << "<blockquote>\n";
                in_blockquote = true;
            }
            html << "<p>" << inline_markdown(line.substr(2)) << "</p>\n";
            continue;
        }
        if (in_list) {
            html << "</ul>\n";
            in_list = false;
        }
        if (in_ordered_list) {
            html << "</ol>\n";
            in_ordered_list = false;
        }
        if (in_blockquote) {
            html << "</blockquote>\n";
            in_blockquote = false;
        }
        size_t hashes = 0;
        while (hashes < line.size() && line[hashes] == '#') hashes++;
        if (hashes > 0 && hashes <= 6 && hashes < line.size() && line[hashes] == ' ') {
            html << "<h" << hashes << ">" << inline_markdown(line.substr(hashes + 1)) << "</h" << hashes << ">\n";
        } else if (line.empty()) {
            continue;
        } else {
            html << "<p>" << inline_markdown(line) << "</p>\n";
        }
    }
    flush_table();
    if (in_list) html << "</ul>\n";
    if (in_ordered_list) html << "</ol>\n";
    if (in_blockquote) html << "</blockquote>\n";
    if (in_pre) html << "</code></pre>\n";
    return html.str();
}

void append_md4c_html(const MD_CHAR* data, MD_SIZE size, void* userdata) {
    auto* output = static_cast<std::string*>(userdata);
    output->append(data, data + size);
}

std::string markdown_to_html(const char* md) {
    std::string input = md ? md : "";
    std::string output;
    output.reserve(input.size() + input.size() / 4 + 64);
    unsigned parser_flags = MD_DIALECT_GITHUB | MD_FLAG_PERMISSIVEATXHEADERS | MD_FLAG_NOHTMLSPANS | MD_FLAG_NOHTMLBLOCKS;
    unsigned renderer_flags = MD_HTML_FLAG_SKIP_UTF8_BOM | MD_HTML_FLAG_XHTML;
    int result = md_html(input.data(), static_cast<MD_SIZE>(input.size()), append_md4c_html, &output, parser_flags, renderer_flags);
    if (result == 0) {
        return output;
    }
    return simple_markdown_to_html(md);
}

bool parse_hex_oid(const char* s, BtOid& out) {
    if (!s || std::strlen(s) < 40) return false;
    uint32_t parts[5]{};
    for (int p = 0; p < 5; ++p) {
        uint32_t value = 0;
        for (int i = 0; i < 8; ++i) {
            char c = s[p * 8 + i];
            uint32_t nibble;
            if (c >= '0' && c <= '9') nibble = static_cast<uint32_t>(c - '0');
            else if (c >= 'a' && c <= 'f') nibble = static_cast<uint32_t>(c - 'a' + 10);
            else if (c >= 'A' && c <= 'F') nibble = static_cast<uint32_t>(c - 'A' + 10);
            else return false;
            value = (value << 4) | nibble;
        }
        parts[p] = value;
    }
    out = { parts[0], parts[1], parts[2], parts[3], parts[4] };
    return true;
}

bool parse_hex_oid(const std::string& s, BtOid& out) {
    return parse_hex_oid(s.c_str(), out);
}

bool parse_hex_oid_at(const char* s, size_t len, BtOid& out) {
    if (!s || len < 40) return false;
    uint32_t parts[5]{};
    for (int p = 0; p < 5; ++p) {
        uint32_t value = 0;
        for (int i = 0; i < 8; ++i) {
            char c = s[p * 8 + i];
            uint32_t nibble;
            if (c >= '0' && c <= '9') nibble = static_cast<uint32_t>(c - '0');
            else if (c >= 'a' && c <= 'f') nibble = static_cast<uint32_t>(c - 'a' + 10);
            else if (c >= 'A' && c <= 'F') nibble = static_cast<uint32_t>(c - 'A' + 10);
            else return false;
            value = (value << 4) | nibble;
        }
        parts[p] = value;
    }
    out = { parts[0], parts[1], parts[2], parts[3], parts[4] };
    return true;
}

std::string oid_to_hex(const BtOid& oid) {
    char buffer[41];
    std::snprintf(buffer, sizeof(buffer), "%08x%08x%08x%08x%08x", oid.s0, oid.s1, oid.s2, oid.s3, oid.s4);
    return buffer;
}

std::vector<std::string> split_lines(const std::string& text) {
    std::vector<std::string> lines;
    std::istringstream input(text);
    std::string line;
    while (std::getline(input, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        lines.push_back(line);
    }
    return lines;
}

std::vector<std::string> split_char(const std::string& text, char sep) {
    std::vector<std::string> parts;
    size_t start = 0;
    while (start <= text.size()) {
        size_t end = text.find(sep, start);
        if (end == std::string::npos) end = text.size();
        parts.push_back(text.substr(start, end - start));
        if (end == text.size()) break;
        start = end + 1;
    }
    return parts;
}

std::string trim(const std::string& s) {
    size_t start = 0;
    while (start < s.size() && std::isspace(static_cast<unsigned char>(s[start]))) start++;
    size_t end = s.size();
    while (end > start && std::isspace(static_cast<unsigned char>(s[end - 1]))) end--;
    return s.substr(start, end - start);
}

std::string git_work_tree_from_git_dir(const char* git_dir) {
    std::string value = git_dir ? git_dir : "";
    std::replace(value.begin(), value.end(), '\\', '/');
    if (value.size() >= 5 && value.substr(value.size() - 5) == "/.git") {
        return value.substr(0, value.size() - 5);
    }
    return "";
}

std::string object_path(char* git_dir, const BtOid& oid) {
    std::string hex = oid_to_hex(oid);
    std::string dir = git_dir ? git_dir : "";
    return dir + "/objects/" + hex.substr(0, 2) + "/" + hex.substr(2);
}

std::vector<uint8_t> read_all_bytes(const std::string& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) return {};
    input.seekg(0, std::ios::end);
    std::streamoff size = input.tellg();
    input.seekg(0, std::ios::beg);
    std::vector<uint8_t> bytes(static_cast<size_t>(size));
    if (size > 0) input.read(reinterpret_cast<char*>(bytes.data()), size);
    return bytes;
}

std::string read_all_text(const std::string& path) {
    std::ifstream input(path, std::ios::binary);
    if (!input) return "";
    std::ostringstream output;
    output << input.rdbuf();
    return output.str();
}

bool ensure_directory(const std::string& path) {
    if (path.empty()) return false;
    std::string normalized = path;
    std::replace(normalized.begin(), normalized.end(), '\\', '/');
    size_t start = 0;
    if (normalized.size() >= 3 && normalized[1] == ':' && normalized[2] == '/') {
        start = 3;
    } else if (normalized.size() >= 2 && normalized[0] == '/' && normalized[1] == '/') {
        start = normalized.find('/', 2);
        if (start == std::string::npos) start = 2;
    }
    for (size_t pos = normalized.find('/', start); pos != std::string::npos; pos = normalized.find('/', pos + 1)) {
        std::string part = normalized.substr(0, pos);
        if (!part.empty()) {
            CreateDirectoryA(part.c_str(), nullptr);
        }
    }
    return CreateDirectoryA(normalized.c_str(), nullptr) || GetLastError() == ERROR_ALREADY_EXISTS;
}

struct ZStream {
    uint8_t* next_in;
    unsigned int avail_in;
    unsigned long total_in;
    uint8_t* next_out;
    unsigned int avail_out;
    unsigned long total_out;
    char* msg;
    void* state;
    void* zalloc;
    void* zfree;
    void* opaque;
    int data_type;
    unsigned long adler;
    unsigned long reserved;
};

typedef const char* (__cdecl* ZlibVersionFn)();
typedef int (__cdecl* InflateInitFn)(ZStream*, const char*, int);
typedef int (__cdecl* InflateFn)(ZStream*, int);
typedef int (__cdecl* InflateEndFn)(ZStream*);
typedef int (__cdecl* InflateResetFn)(ZStream*);

extern "C" const char* zlibVersion();
extern "C" int inflateInit_(ZStream*, const char*, int);
extern "C" int inflate(ZStream*, int);
extern "C" int inflateEnd(ZStream*);
extern "C" int inflateReset(ZStream*);

struct ZlibApi {
    HMODULE module = nullptr;
    ZlibVersionFn version = nullptr;
    InflateInitFn inflate_init = nullptr;
    InflateFn inflate = nullptr;
    InflateEndFn inflate_end = nullptr;
    InflateResetFn inflate_reset = nullptr;
};

ZlibApi& zlib_api() {
    static ZlibApi api;
    static bool initialized = false;
    if (initialized) return api;
    initialized = true;
    api.module = reinterpret_cast<HMODULE>(1);
    api.version = zlibVersion;
    api.inflate_init = inflateInit_;
    api.inflate = inflate;
    api.inflate_end = inflateEnd;
    api.inflate_reset = inflateReset;
    return api;
    std::vector<std::wstring> candidates = {
        L"zlib1.dll",
        L"C:\\Program Files\\Git\\mingw64\\bin\\zlib1.dll",
        L"C:\\Program Files\\Git\\usr\\bin\\zlib1.dll"
    };
    for (const auto& candidate : candidates) {
        api.module = LoadLibraryW(candidate.c_str());
        if (api.module) break;
    }
    if (!api.module) return api;
    api.version = reinterpret_cast<ZlibVersionFn>(GetProcAddress(api.module, "zlibVersion"));
    api.inflate_init = reinterpret_cast<InflateInitFn>(GetProcAddress(api.module, "inflateInit_"));
    api.inflate = reinterpret_cast<InflateFn>(GetProcAddress(api.module, "inflate"));
    api.inflate_end = reinterpret_cast<InflateEndFn>(GetProcAddress(api.module, "inflateEnd"));
    api.inflate_reset = reinterpret_cast<InflateResetFn>(GetProcAddress(api.module, "inflateReset"));
    return api;
}

bool inflate_zlib_dynamic(const uint8_t* compressed_data, size_t compressed_size, std::vector<uint8_t>& out) {
    ZlibApi& api = zlib_api();
    if (!api.module || !api.version || !api.inflate_init || !api.inflate || !api.inflate_end) return false;
    out.clear();
    thread_local ZStream stream{};
    thread_local bool stream_initialized = false;
    if (!stream_initialized) {
        if (api.inflate_init(&stream, api.version(), sizeof(ZStream)) != 0) return false;
        stream_initialized = true;
    } else if (api.inflate_reset) {
        if (api.inflate_reset(&stream) != 0) return false;
    } else {
        api.inflate_end(&stream);
        stream = ZStream{};
        if (api.inflate_init(&stream, api.version(), sizeof(ZStream)) != 0) {
            stream_initialized = false;
            return false;
        }
    }
    stream.next_in = const_cast<uint8_t*>(compressed_data);
    stream.avail_in = static_cast<unsigned int>(compressed_size);
    uint8_t buffer[8192];
    int ret = 0;
    do {
        stream.next_out = buffer;
        stream.avail_out = sizeof(buffer);
        ret = api.inflate(&stream, 0);
        if (ret != 0 && ret != 1) {
            if (api.inflate_reset) api.inflate_reset(&stream);
            return false;
        }
        out.insert(out.end(), buffer, buffer + (sizeof(buffer) - stream.avail_out));
    } while (ret != 1);
    return true;
}

bool inflate_zlib_dynamic(const std::vector<uint8_t>& compressed, std::vector<uint8_t>& out) {
    if (compressed.empty()) return false;
    return inflate_zlib_dynamic(compressed.data(), compressed.size(), out);
}

bool inflate_zlib_dynamic_sized(const uint8_t* compressed_data, size_t compressed_size, size_t expected_size, std::vector<uint8_t>& out) {
    ZlibApi& api = zlib_api();
    if (!api.module || !api.inflate_init || !api.inflate || !api.inflate_end || !compressed_data) return false;
    ZStream stream{};
    stream.next_in = const_cast<uint8_t*>(compressed_data);
    stream.avail_in = static_cast<unsigned int>((std::min)(compressed_size, static_cast<size_t>(UINT_MAX)));
    const char* version = api.version ? api.version() : "1.2.11";
    if (api.inflate_init(&stream, version, sizeof(ZStream)) != 0) return false;
    out.assign(expected_size, 0);
    stream.next_out = out.empty() ? nullptr : out.data();
    stream.avail_out = static_cast<unsigned int>((std::min)(expected_size, static_cast<size_t>(UINT_MAX)));
    int ret = api.inflate(&stream, 4);
    if (ret != 1 || stream.total_out > expected_size) {
        api.inflate_end(&stream);
        out.clear();
        return false;
    }
    out.resize(stream.total_out);
    api.inflate_end(&stream);
    return true;
}

struct GitObject {
    std::string type;
    std::vector<uint8_t> data;
};

struct PackIndexEntry {
    std::string pack_path;
    uint64_t offset = 0;
    uint64_t next_offset = 0;
};

struct CachedCommitEdges {
    std::vector<BtOid> parents;
    int64_t author_time = 0;
    int64_t committer_time = 0;
};

struct MappedPack {
    HANDLE file = INVALID_HANDLE_VALUE;
    HANDLE mapping = nullptr;
    uint8_t* data = nullptr;
    size_t size = 0;

    ~MappedPack() {
        if (data) UnmapViewOfFile(data);
        if (mapping) CloseHandle(mapping);
        if (file != INVALID_HANDLE_VALUE) CloseHandle(file);
    }
};

struct RepositoryObjectCache {
    std::recursive_mutex mutex;
    bool commit_graph_loaded = false;
    bool pack_indexes_loaded = false;
    bool persistent_commit_cache_loaded = false;
    bool persistent_commit_cache_valid = false;
    bool persistent_commit_cache_dirty = false;
    bool object_dirs_loaded = false;
    uint64_t pack_signature = 1469598103934665603ull;
    std::vector<std::string> object_dirs;
    std::unordered_map<BtOid, PackIndexEntry, BtOidHash, BtOidEqual> pack_index;
    std::unordered_map<std::string, std::shared_ptr<MappedPack>> pack_maps;
    std::unordered_map<std::string, std::unordered_map<uint64_t, uint64_t>> pack_next_offsets;
    std::unordered_map<std::string, std::unordered_map<uint64_t, BtOid>> pack_offset_oids;
    std::unordered_set<std::string> preloaded_commit_edge_packs;
    std::unordered_map<BtOid, GitObject, BtOidHash, BtOidEqual> objects;
    std::unordered_map<std::string, std::unordered_map<uint64_t, GitObject>> pack_offset_objects;
    std::unordered_map<BtOid, CachedCommitEdges, BtOidHash, BtOidEqual> commit_edges;
    std::unordered_map<BtOid, int64_t, BtOidHash, BtOidEqual> committer_times;
    std::unordered_map<BtOid, std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>>, BtOidHash, BtOidEqual> reachable_commits;
};

std::map<std::string, RepositoryObjectCache> g_object_caches;
std::recursive_mutex g_object_caches_mutex;

RepositoryObjectCache& object_cache(char* git_dir) {
    std::lock_guard<std::recursive_mutex> lock(g_object_caches_mutex);
    return g_object_caches[git_dir ? git_dir : ""];
}

RepositoryObjectCache& object_cache(char* git_dir, BtCommitGraphCache* cache) {
    if (cache && cache->inner) {
        return *static_cast<RepositoryObjectCache*>(cache->inner);
    }
    return object_cache(git_dir);
}

bool is_absolute_path(const std::string& path) {
    if (path.size() >= 3 && std::isalpha(static_cast<unsigned char>(path[0])) && path[1] == ':' && (path[2] == '\\' || path[2] == '/')) return true;
    if (path.size() >= 2 && ((path[0] == '\\' && path[1] == '\\') || (path[0] == '/' && path[1] == '/'))) return true;
    if (!path.empty() && (path[0] == '/' || path[0] == '\\')) return true;
    return false;
}

std::string parent_path(const std::string& path) {
    size_t pos = path.find_last_of("/\\");
    if (pos == std::string::npos) return "";
    return path.substr(0, pos);
}

std::string join_path_string(const std::string& left, const std::string& right) {
    if (left.empty()) return right;
    if (right.empty()) return left;
    char last = left.back();
    if (last == '/' || last == '\\') return left + right;
    return left + "/" + right;
}

void collect_object_dir_recursive(const std::string& object_dir, RepositoryObjectCache& cache, std::unordered_set<std::string>& visited) {
    if (object_dir.empty()) return;
    std::string normalized = object_dir;
    std::replace(normalized.begin(), normalized.end(), '\\', '/');
    if (!visited.insert(normalized).second) return;
    cache.object_dirs.push_back(normalized);
    std::string alternates = read_all_text(join_path_string(normalized, "info/alternates"));
    std::string alternates_parent = parent_path(join_path_string(normalized, "info/alternates"));
    for (const std::string& raw_line : split_lines(alternates)) {
        std::string line = trim(raw_line);
        if (line.empty() || line[0] == '#') continue;
        std::string alternate = is_absolute_path(line) ? line : join_path_string(alternates_parent, line);
        collect_object_dir_recursive(alternate, cache, visited);
    }
}

void load_object_dirs(char* git_dir, RepositoryObjectCache& cache) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    if (cache.object_dirs_loaded) return;
    cache.object_dirs_loaded = true;
    std::unordered_set<std::string> visited;
    collect_object_dir_recursive(join_path_string(git_dir ? git_dir : "", "objects"), cache, visited);
}

bool read_loose_object(char* git_dir, const BtOid& oid, GitObject& object) {
    RepositoryObjectCache& cache = object_cache(git_dir);
    load_object_dirs(git_dir, cache);
    std::string hex = oid_to_hex(oid);
    std::vector<uint8_t> compressed;
    for (const std::string& object_dir : cache.object_dirs) {
        compressed = read_all_bytes(join_path_string(join_path_string(object_dir, hex.substr(0, 2)), hex.substr(2)));
        if (!compressed.empty()) break;
    }
    if (compressed.empty()) return false;
    std::vector<uint8_t> inflated;
    if (!inflate_zlib_dynamic(compressed, inflated)) return false;
    auto nul = std::find(inflated.begin(), inflated.end(), 0);
    if (nul == inflated.end()) return false;
    std::string header(inflated.begin(), nul);
    size_t space = header.find(' ');
    if (space == std::string::npos) return false;
    object.type = header.substr(0, space);
    object.data.assign(nul + 1, inflated.end());
    return true;
}

std::shared_ptr<MappedPack> map_pack_file(const std::string& path) {
    auto mapped = std::make_shared<MappedPack>();
    mapped->file = CreateFileA(path.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (mapped->file == INVALID_HANDLE_VALUE) return nullptr;
    LARGE_INTEGER size{};
    if (!GetFileSizeEx(mapped->file, &size) || size.QuadPart <= 0) return nullptr;
    mapped->size = static_cast<size_t>(size.QuadPart);
    mapped->mapping = CreateFileMappingA(mapped->file, nullptr, PAGE_READONLY, 0, 0, nullptr);
    if (!mapped->mapping) return nullptr;
    mapped->data = static_cast<uint8_t*>(MapViewOfFile(mapped->mapping, FILE_MAP_READ, 0, 0, 0));
    if (!mapped->data) return nullptr;
    return mapped;
}

std::shared_ptr<MappedPack> get_mapped_pack(RepositoryObjectCache& cache, const std::string& pack_path) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    auto found = cache.pack_maps.find(pack_path);
    if (found != cache.pack_maps.end()) {
        return found->second;
    }
    auto mapped = map_pack_file(pack_path);
    if (mapped) {
        cache.pack_maps[pack_path] = mapped;
    }
    return mapped;
}

uint32_t read_be32(const uint8_t* p) {
    return (static_cast<uint32_t>(p[0]) << 24) | (static_cast<uint32_t>(p[1]) << 16) | (static_cast<uint32_t>(p[2]) << 8) | p[3];
}

uint64_t read_be64(const uint8_t* p) {
    uint64_t value = 0;
    for (int i = 0; i < 8; ++i) value = (value << 8) | p[i];
    return value;
}

void append_u32(std::vector<uint8_t>& out, uint32_t value) {
    out.push_back(static_cast<uint8_t>(value & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 16) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 24) & 0xff));
}

void append_i64(std::vector<uint8_t>& out, int64_t value) {
    uint64_t raw = static_cast<uint64_t>(value);
    for (int i = 0; i < 8; ++i) {
        out.push_back(static_cast<uint8_t>((raw >> (i * 8)) & 0xff));
    }
}

uint32_t read_le32(const uint8_t* p) {
    return static_cast<uint32_t>(p[0]) | (static_cast<uint32_t>(p[1]) << 8) | (static_cast<uint32_t>(p[2]) << 16) | (static_cast<uint32_t>(p[3]) << 24);
}

uint64_t read_le64(const uint8_t* p) {
    uint64_t value = 0;
    for (int i = 7; i >= 0; --i) {
        value = (value << 8) | p[i];
    }
    return value;
}

void hash_mix(uint64_t& hash, const void* data, size_t len) {
    const uint8_t* bytes = static_cast<const uint8_t*>(data);
    for (size_t i = 0; i < len; ++i) {
        hash ^= bytes[i];
        hash *= 1099511628211ull;
    }
}

void oid_to_raw20(const BtOid& oid, uint8_t raw[20]) {
    uint32_t parts[5] = { oid.s0, oid.s1, oid.s2, oid.s3, oid.s4 };
    for (int i = 0; i < 5; ++i) {
        raw[i * 4 + 0] = static_cast<uint8_t>((parts[i] >> 24) & 0xff);
        raw[i * 4 + 1] = static_cast<uint8_t>((parts[i] >> 16) & 0xff);
        raw[i * 4 + 2] = static_cast<uint8_t>((parts[i] >> 8) & 0xff);
        raw[i * 4 + 3] = static_cast<uint8_t>(parts[i] & 0xff);
    }
}

int compare_raw_oid(const uint8_t* lhs, const uint8_t* rhs) {
    return std::memcmp(lhs, rhs, 20);
}

void load_pack_indexes(char* git_dir, RepositoryObjectCache& cache) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    if (cache.pack_indexes_loaded) return;
    cache.pack_indexes_loaded = true;
    load_object_dirs(git_dir, cache);
    for (const std::string& object_dir : cache.object_dirs) {
    std::string pattern = join_path_string(object_dir, "pack/*.idx");
    WIN32_FIND_DATAA data{};
    HANDLE find = FindFirstFileA(pattern.c_str(), &data);
    if (find == INVALID_HANDLE_VALUE) continue;
    std::string pack_dir = join_path_string(object_dir, "pack/");
    do {
        std::string idx_path = pack_dir + data.cFileName;
        hash_mix(cache.pack_signature, data.cFileName, std::strlen(data.cFileName));
        hash_mix(cache.pack_signature, &data.nFileSizeLow, sizeof(data.nFileSizeLow));
        hash_mix(cache.pack_signature, &data.nFileSizeHigh, sizeof(data.nFileSizeHigh));
        hash_mix(cache.pack_signature, &data.ftLastWriteTime.dwLowDateTime, sizeof(data.ftLastWriteTime.dwLowDateTime));
        hash_mix(cache.pack_signature, &data.ftLastWriteTime.dwHighDateTime, sizeof(data.ftLastWriteTime.dwHighDateTime));
        std::vector<uint8_t> idx = read_all_bytes(idx_path);
        if (idx.size() < 8 + 256 * 4) continue;
        size_t fanout_offset = 0;
        if (read_be32(idx.data()) == 0xff744f63u) {
            if (read_be32(idx.data() + 4) != 2) continue;
            fanout_offset = 8;
        } else {
            fanout_offset = 0;
        }
        uint32_t count = read_be32(idx.data() + fanout_offset + 255 * 4);
        size_t names_offset = fanout_offset + 256 * 4;
        size_t crc_offset = names_offset + static_cast<size_t>(count) * 20;
        size_t offsets_offset = crc_offset + static_cast<size_t>(count) * 4;
        size_t large_offsets_offset = offsets_offset + static_cast<size_t>(count) * 4;
        if (idx.size() < offsets_offset + static_cast<size_t>(count) * 4) continue;
        std::string pack_path = idx_path.substr(0, idx_path.size() - 4) + ".pack";
        std::vector<std::pair<BtOid, uint64_t>> entries;
        entries.reserve(count);
        std::vector<uint64_t> offsets;
        offsets.reserve(count);
        for (uint32_t i = 0; i < count; ++i) {
            BtOid oid = oid_from_raw20(idx.data() + names_offset + static_cast<size_t>(i) * 20);
            uint32_t raw_offset = read_be32(idx.data() + offsets_offset + static_cast<size_t>(i) * 4);
            uint64_t offset = 0;
            if ((raw_offset & 0x80000000u) == 0) {
                offset = raw_offset;
            } else {
                uint32_t large_index = raw_offset & 0x7fffffffu;
                size_t pos = large_offsets_offset + static_cast<size_t>(large_index) * 8;
                if (idx.size() < pos + 8) {
                    continue;
                }
                offset = read_be64(idx.data() + pos);
            }
            entries.emplace_back(oid, offset);
            offsets.push_back(offset);
        }
        std::sort(offsets.begin(), offsets.end());
        std::unordered_map<uint64_t, uint64_t>& next_offsets = cache.pack_next_offsets[pack_path];
        std::unordered_map<uint64_t, BtOid>& offset_oids = cache.pack_offset_oids[pack_path];
        next_offsets.reserve(offsets.size());
        offset_oids.reserve(entries.size());
        for (size_t i = 0; i < offsets.size(); ++i) {
            uint64_t next_offset = (i + 1 < offsets.size()) ? offsets[i + 1] : 0;
            next_offsets[offsets[i]] = next_offset;
        }
        for (const auto& entry : entries) {
            uint64_t next_offset = 0;
            auto next_found = next_offsets.find(entry.second);
            if (next_found != next_offsets.end()) {
                next_offset = next_found->second;
            }
            cache.pack_index[entry.first] = PackIndexEntry{ pack_path, entry.second, next_offset };
            offset_oids[entry.second] = entry.first;
        }
    } while (FindNextFileA(find, &data));
    FindClose(find);
    }
}

bool load_commit_graph_file(const std::string& path, RepositoryObjectCache& cache) {
    std::vector<uint8_t> graph = read_all_bytes(path);
    if (graph.size() < 8 || std::memcmp(graph.data(), "CGPH", 4) != 0) return false;
    uint8_t version = graph[4];
    uint8_t hash_id = graph[5];
    uint8_t chunk_count = graph[6];
    if (version != 1 || hash_id != 1 || chunk_count == 0) return false;

    std::map<std::string, uint64_t> chunks;
    size_t table_offset = 8;
    for (uint8_t i = 0; i <= chunk_count; ++i) {
        if (table_offset + 12 > graph.size()) return false;
        std::string id(reinterpret_cast<char*>(graph.data() + table_offset), 4);
        uint64_t offset = read_be64(graph.data() + table_offset + 4);
        table_offset += 12;
        if (id != "\0\0\0\0") {
            chunks[id] = offset;
        }
    }
    auto oidf_it = chunks.find("OIDF");
    auto oidl_it = chunks.find("OIDL");
    auto cdat_it = chunks.find("CDAT");
    if (oidf_it == chunks.end() || oidl_it == chunks.end() || cdat_it == chunks.end()) return false;
    uint64_t oidf = oidf_it->second;
    uint64_t oidl = oidl_it->second;
    uint64_t cdat = cdat_it->second;
    if (oidf + 256ull * 4 > graph.size()) return false;
    uint32_t commit_count = read_be32(graph.data() + oidf + 255ull * 4);
    if (oidl + static_cast<uint64_t>(commit_count) * 20 > graph.size()) return false;
    constexpr uint32_t ParentNone = 0x70000000u;
    constexpr uint32_t ParentExtra = 0x80000000u;
    constexpr uint32_t ParentMask = 0x7fffffffu;
    const uint64_t entry_size = 20 + 4 + 4 + 4 + 4;
    if (cdat + static_cast<uint64_t>(commit_count) * entry_size > graph.size()) return false;
    std::vector<BtOid> oids;
    oids.reserve(commit_count);
    for (uint32_t i = 0; i < commit_count; ++i) {
        oids.push_back(oid_from_raw20(graph.data() + oidl + static_cast<uint64_t>(i) * 20));
    }
    uint64_t edge_offset = 0;
    auto edge_it = chunks.find("EDGE");
    if (edge_it != chunks.end()) {
        edge_offset = edge_it->second;
    }
    for (uint32_t i = 0; i < commit_count; ++i) {
        const uint8_t* entry = graph.data() + cdat + static_cast<uint64_t>(i) * entry_size;
        uint32_t parent1 = read_be32(entry + 20);
        uint32_t parent2 = read_be32(entry + 24);
        uint32_t generation_and_time_hi = read_be32(entry + 28);
        uint32_t time_low = read_be32(entry + 32);
        CachedCommitEdges edges;
        edges.author_time = (static_cast<int64_t>(generation_and_time_hi >> 30) << 32) | time_low;
        edges.committer_time = edges.author_time;
        if (parent1 != ParentNone && parent1 < oids.size()) {
            edges.parents.push_back(oids[parent1]);
        }
        if (parent2 != ParentNone) {
            if ((parent2 & ParentExtra) != 0 && edge_offset != 0) {
                uint32_t edge_index = parent2 & ParentMask;
                while (edge_offset + static_cast<uint64_t>(edge_index) * 4 + 4 <= graph.size()) {
                    uint32_t edge = read_be32(graph.data() + edge_offset + static_cast<uint64_t>(edge_index) * 4);
                    uint32_t parent_index = edge & ParentMask;
                    if (parent_index < oids.size()) {
                        edges.parents.push_back(oids[parent_index]);
                    }
                    ++edge_index;
                    if ((edge & ParentExtra) != 0) break;
                }
            } else if (parent2 < oids.size()) {
                edges.parents.push_back(oids[parent2]);
            }
        }
        cache.commit_edges.emplace(oids[i], std::move(edges));
    }
    return true;
}

void load_commit_graph(char* git_dir, RepositoryObjectCache& cache) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    if (cache.commit_graph_loaded) return;
    cache.commit_graph_loaded = true;
    std::string base = git_dir ? git_dir : "";
    if (load_commit_graph_file(base + "/objects/info/commit-graph", cache)) return;
    std::string chain = read_all_text(base + "/objects/info/commit-graphs/commit-graph-chain");
    for (const std::string& line : split_lines(chain)) {
        std::string name = trim(line);
        if (name.empty()) continue;
        load_commit_graph_file(base + "/objects/info/commit-graphs/graph-" + name + ".graph", cache);
    }
    load_object_dirs(git_dir, cache);
    for (const std::string& object_dir : cache.object_dirs) {
        if (object_dir == join_path_string(base, "objects")) continue;
        if (load_commit_graph_file(join_path_string(object_dir, "info/commit-graph"), cache)) continue;
        std::string alt_chain = read_all_text(join_path_string(object_dir, "info/commit-graphs/commit-graph-chain"));
        for (const std::string& line : split_lines(alt_chain)) {
            std::string name = trim(line);
            if (name.empty()) continue;
            load_commit_graph_file(join_path_string(object_dir, "info/commit-graphs/graph-" + name + ".graph"), cache);
        }
    }
}

std::string persistent_commit_cache_path(char* git_dir) {
    char local_app_data[MAX_PATH]{};
    DWORD len = GetEnvironmentVariableA("LOCALAPPDATA", local_app_data, static_cast<DWORD>(sizeof(local_app_data)));
    std::string root = (len > 0 && len < sizeof(local_app_data)) ? local_app_data : ".";
    std::string cache_dir = join_path_string(root, "ForkPlusData/BiturboCommitCache");
    ensure_directory(cache_dir);
    uint64_t hash = 1469598103934665603ull;
    std::string key = git_dir ? git_dir : "";
    std::replace(key.begin(), key.end(), '\\', '/');
    hash_mix(hash, key.data(), key.size());
    char filename[64];
    std::snprintf(filename, sizeof(filename), "%016llx-v1.bin", static_cast<unsigned long long>(hash));
    return join_path_string(cache_dir, filename);
}

bool load_persistent_commit_cache(char* git_dir, RepositoryObjectCache& cache) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    if (cache.persistent_commit_cache_loaded) return cache.persistent_commit_cache_valid;
    cache.persistent_commit_cache_loaded = true;
    load_pack_indexes(git_dir, cache);
    std::vector<uint8_t> bytes = read_all_bytes(persistent_commit_cache_path(git_dir));
    const char magic[] = "FPBCMC1";
    if (bytes.size() < sizeof(magic) - 1 + 8 + 4) return false;
    if (bytes.size() > 128ull * 1024ull * 1024ull) return false;
    size_t pos = 0;
    if (std::memcmp(bytes.data(), magic, sizeof(magic) - 1) != 0) return false;
    pos += sizeof(magic) - 1;
    uint64_t signature = read_le64(bytes.data() + pos);
    pos += 8;
    if (signature != cache.pack_signature) return false;
    uint32_t count = read_le32(bytes.data() + pos);
    pos += 4;
    if (count > 5000000u) return false;
    for (uint32_t i = 0; i < count; ++i) {
        if (pos + 20 + 8 + 8 + 4 > bytes.size()) {
            return false;
        }
        BtOid oid = oid_from_raw20(bytes.data() + pos);
        pos += 20;
        CachedCommitEdges edges;
        edges.author_time = static_cast<int64_t>(read_le64(bytes.data() + pos));
        pos += 8;
        edges.committer_time = static_cast<int64_t>(read_le64(bytes.data() + pos));
        pos += 8;
        uint32_t parent_count = read_le32(bytes.data() + pos);
        pos += 4;
        if (parent_count > 128 || pos + static_cast<size_t>(parent_count) * 20 > bytes.size()) {
            return false;
        }
        edges.parents.reserve(parent_count);
        for (uint32_t j = 0; j < parent_count; ++j) {
            edges.parents.push_back(oid_from_raw20(bytes.data() + pos));
            pos += 20;
        }
        cache.commit_edges.emplace(oid, std::move(edges));
    }
    cache.persistent_commit_cache_valid = true;
    return true;
}

void save_persistent_commit_cache(char* git_dir, RepositoryObjectCache& cache) {
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    if (!cache.persistent_commit_cache_dirty || cache.commit_edges.empty()) return;
    std::vector<uint8_t> bytes;
    const char magic[] = "FPBCMC1";
    bytes.insert(bytes.end(), magic, magic + sizeof(magic) - 1);
    append_i64(bytes, static_cast<int64_t>(cache.pack_signature));
    append_u32(bytes, static_cast<uint32_t>(cache.commit_edges.size()));
    for (const auto& item : cache.commit_edges) {
        uint8_t raw[20];
        oid_to_raw20(item.first, raw);
        bytes.insert(bytes.end(), raw, raw + 20);
        append_i64(bytes, item.second.author_time);
        append_i64(bytes, item.second.committer_time);
        append_u32(bytes, static_cast<uint32_t>(item.second.parents.size()));
        for (const BtOid& parent : item.second.parents) {
            oid_to_raw20(parent, raw);
            bytes.insert(bytes.end(), raw, raw + 20);
        }
    }
    std::string path = persistent_commit_cache_path(git_dir);
    std::ofstream output(path, std::ios::binary | std::ios::trunc);
    if (!output) return;
    output.write(reinterpret_cast<const char*>(bytes.data()), static_cast<std::streamsize>(bytes.size()));
    cache.persistent_commit_cache_dirty = false;
}

bool should_use_git_for_commits(char* git_dir_path, bool date_order, int64_t skip_pages, BtCommitGraphCache* cache_handle) {
    return false;
}

bool find_pack_offset(char* git_dir, const BtOid& oid, std::string& pack_path, uint64_t& offset, BtCommitGraphCache* cache_handle = nullptr) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    load_pack_indexes(git_dir, cache);
    auto found = cache.pack_index.find(oid);
    if (found == cache.pack_index.end()) return false;
    pack_path = found->second.pack_path;
    offset = found->second.offset;
    return true;
}

uint64_t find_next_pack_offset(RepositoryObjectCache& cache, const std::string& pack_path, uint64_t offset) {
    auto pack_found = cache.pack_next_offsets.find(pack_path);
    if (pack_found == cache.pack_next_offsets.end()) return 0;
    auto offset_found = pack_found->second.find(offset);
    if (offset_found == pack_found->second.end()) return 0;
    return offset_found->second;
}

bool read_pack_object_at(char* git_dir, const std::string& pack_path, uint64_t offset, GitObject& object, BtCommitGraphCache* cache_handle = nullptr);

bool apply_delta(const std::vector<uint8_t>& base, const std::vector<uint8_t>& delta, std::vector<uint8_t>& out) {
    size_t pos = 0;
    auto read_var = [&]() -> uint64_t {
        uint64_t value = 0;
        int shift = 0;
        while (pos < delta.size()) {
            uint8_t c = delta[pos++];
            value |= static_cast<uint64_t>(c & 0x7f) << shift;
            if ((c & 0x80) == 0) break;
            shift += 7;
        }
        return value;
    };
    read_var();
    uint64_t target_size = read_var();
    out.clear();
    out.reserve(static_cast<size_t>(target_size));
    while (pos < delta.size()) {
        uint8_t cmd = delta[pos++];
        if (cmd & 0x80) {
            uint32_t cp_off = 0;
            uint32_t cp_size = 0;
            if (cmd & 0x01) cp_off |= delta[pos++];
            if (cmd & 0x02) cp_off |= static_cast<uint32_t>(delta[pos++]) << 8;
            if (cmd & 0x04) cp_off |= static_cast<uint32_t>(delta[pos++]) << 16;
            if (cmd & 0x08) cp_off |= static_cast<uint32_t>(delta[pos++]) << 24;
            if (cmd & 0x10) cp_size |= delta[pos++];
            if (cmd & 0x20) cp_size |= static_cast<uint32_t>(delta[pos++]) << 8;
            if (cmd & 0x40) cp_size |= static_cast<uint32_t>(delta[pos++]) << 16;
            if (cp_size == 0) cp_size = 0x10000;
            if (static_cast<size_t>(cp_off) + cp_size > base.size()) return false;
            out.insert(out.end(), base.begin() + cp_off, base.begin() + cp_off + cp_size);
        } else if (cmd != 0) {
            if (pos + cmd > delta.size()) return false;
            out.insert(out.end(), delta.begin() + pos, delta.begin() + pos + cmd);
            pos += cmd;
        } else {
            return false;
        }
    }
    return out.size() == target_size;
}

bool read_pack_object_at(char* git_dir, const std::string& pack_path, uint64_t offset, GitObject& object, BtCommitGraphCache* cache_handle) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    std::unordered_map<uint64_t, GitObject>& offset_objects = cache.pack_offset_objects[pack_path];
    auto cached = offset_objects.find(offset);
    if (cached != offset_objects.end()) {
        object = cached->second;
        return true;
    }
    std::shared_ptr<MappedPack> mapped = get_mapped_pack(cache, pack_path);
    if (!mapped || mapped->size < offset + 1) return false;
    uint8_t* pack = mapped->data;
    size_t pack_size = mapped->size;
    size_t pos = static_cast<size_t>(offset);
    uint8_t c = pack[pos++];
    int type = (c >> 4) & 0x07;
    uint64_t size = c & 0x0f;
    int shift = 4;
    while (c & 0x80) {
        if (pos >= pack_size) return false;
        c = pack[pos++];
        size |= static_cast<uint64_t>(c & 0x7f) << shift;
        shift += 7;
    }
    uint64_t base_offset = 0;
    BtOid base_oid{};
    bool has_base_offset = false;
    bool has_base_oid = false;
    if (type == 6) {
        if (pos >= pack_size) return false;
        c = pack[pos++];
        base_offset = c & 0x7f;
        while (c & 0x80) {
            if (pos >= pack_size) return false;
            c = pack[pos++];
            base_offset = ((base_offset + 1) << 7) | (c & 0x7f);
        }
        base_offset = offset - base_offset;
        has_base_offset = true;
    } else if (type == 7) {
        if (pos + 20 > pack_size) return false;
        base_oid = oid_from_raw20(pack + pos);
        pos += 20;
        has_base_oid = true;
    }
	std::vector<uint8_t> inflated;
    uint64_t next_offset = find_next_pack_offset(cache, pack_path, offset);
    size_t compressed_end = (next_offset > pos && next_offset <= pack_size) ? static_cast<size_t>(next_offset) : pack_size;
	if (!inflate_zlib_dynamic(pack + pos, compressed_end - pos, inflated)) return false;
    if (type == 1) object.type = "commit";
    else if (type == 2) object.type = "tree";
    else if (type == 3) object.type = "blob";
    else if (type == 4) object.type = "tag";
    else if (type == 6 || type == 7) {
        GitObject base;
        if (has_base_offset) {
            if (!read_pack_object_at(git_dir, pack_path, base_offset, base, cache_handle)) return false;
        } else if (has_base_oid) {
            if (!read_git_object(git_dir, base_oid, base, cache_handle)) return false;
        }
        object.type = base.type;
        if (!apply_delta(base.data, inflated, object.data)) return false;
        offset_objects[offset] = object;
        return true;
    } else {
        return false;
    }
    object.data = std::move(inflated);
    offset_objects[offset] = object;
    return true;
}

const GitObject* get_git_object_cached(char* git_dir, const BtOid& oid, BtCommitGraphCache* cache_handle) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    auto cached = cache.objects.find(oid);
    if (cached != cache.objects.end()) {
        return &cached->second;
    }
    GitObject object;
    if (read_loose_object(git_dir, oid, object)) {
        auto inserted = cache.objects.emplace(oid, std::move(object));
        return &inserted.first->second;
    }
    std::string pack_path;
    uint64_t offset = 0;
    if (find_pack_offset(git_dir, oid, pack_path, offset, cache_handle)) {
        if (read_pack_object_at(git_dir, pack_path, offset, object, cache_handle)) {
            auto inserted = cache.objects.emplace(oid, std::move(object));
            return &inserted.first->second;
        }
    }
    return nullptr;
}

bool read_git_object(char* git_dir, const BtOid& oid, GitObject& object, BtCommitGraphCache* cache_handle) {
    const GitObject* cached = get_git_object_cached(git_dir, oid, cache_handle);
    if (!cached) {
        return false;
    }
    object = *cached;
    return true;
}

BtOid oid_from_raw20(const uint8_t* raw) {
    BtOid oid{};
    oid.s0 = (static_cast<uint32_t>(raw[0]) << 24) | (static_cast<uint32_t>(raw[1]) << 16) | (static_cast<uint32_t>(raw[2]) << 8) | raw[3];
    oid.s1 = (static_cast<uint32_t>(raw[4]) << 24) | (static_cast<uint32_t>(raw[5]) << 16) | (static_cast<uint32_t>(raw[6]) << 8) | raw[7];
    oid.s2 = (static_cast<uint32_t>(raw[8]) << 24) | (static_cast<uint32_t>(raw[9]) << 16) | (static_cast<uint32_t>(raw[10]) << 8) | raw[11];
    oid.s3 = (static_cast<uint32_t>(raw[12]) << 24) | (static_cast<uint32_t>(raw[13]) << 16) | (static_cast<uint32_t>(raw[14]) << 8) | raw[15];
    oid.s4 = (static_cast<uint32_t>(raw[16]) << 24) | (static_cast<uint32_t>(raw[17]) << 16) | (static_cast<uint32_t>(raw[18]) << 8) | raw[19];
    return oid;
}

struct CommitInfo {
    std::vector<BtOid> parents;
    std::string author_name;
    std::string author_email;
    int64_t author_time = 0;
    std::string subject;
    std::string body;
};

struct CommitRecord {
    BtOid oid;
    std::vector<BtOid> parents;
    int64_t time = 0;
};

bool parse_person_line(const std::string& value, std::string& name, std::string& email, int64_t& time) {
    size_t lt = value.find('<');
    size_t gt = value.find('>');
    if (lt == std::string::npos || gt == std::string::npos || gt <= lt) return false;
    name = trim(value.substr(0, lt));
    email = value.substr(lt + 1, gt - lt - 1);
    time = _strtoi64(value.substr(gt + 1).c_str(), nullptr, 10);
    return true;
}

bool parse_commit_object(char* git_dir, const BtOid& oid, CommitInfo& info, BtCommitGraphCache* cache_handle = nullptr) {
    const GitObject* object = get_git_object_cached(git_dir, oid, cache_handle);
    if (!object || object->type != "commit") return false;
    std::string content(reinterpret_cast<const char*>(object->data.data()), object->data.size());
    std::istringstream input(content);
    std::string line;
    bool in_message = false;
    std::string message;
    while (std::getline(input, line)) {
        if (!line.empty() && line.back() == '\r') line.pop_back();
        if (in_message) {
            message += line;
            message += "\n";
            continue;
        }
        if (line.empty()) {
            in_message = true;
            continue;
        }
        if (starts_with(line, "parent ")) {
            BtOid parent{};
            if (parse_hex_oid(line.substr(7), parent)) info.parents.push_back(parent);
        } else if (starts_with(line, "author ")) {
            parse_person_line(line.substr(7), info.author_name, info.author_email, info.author_time);
        }
    }
    while (!message.empty() && (message.back() == '\n' || message.back() == '\r')) message.pop_back();
    size_t first_newline = message.find('\n');
    info.subject = first_newline == std::string::npos ? message : message.substr(0, first_newline);
    info.body = first_newline == std::string::npos ? "" : trim(message.substr(first_newline + 1));
    return true;
}

void parse_commit_edges_from_data(const uint8_t* bytes, size_t len, CachedCommitEdges& edges) {
    const char* data = reinterpret_cast<const char*>(bytes);
    size_t pos = 0;
    while (pos < len) {
        size_t line_end = pos;
        while (line_end < len && data[line_end] != '\n') line_end++;
        if (line_end == pos) {
            break;
        }
        size_t line_len = line_end - pos;
        if (line_len >= 47 && std::memcmp(data + pos, "parent ", 7) == 0) {
            BtOid parent{};
            if (parse_hex_oid_at(data + pos + 7, line_len - 7, parent)) {
                edges.parents.push_back(parent);
            }
        } else if (line_len >= 8 && std::memcmp(data + pos, "author ", 7) == 0) {
            const char* gt = static_cast<const char*>(std::memchr(data + pos + 7, '>', line_len - 7));
            if (gt != nullptr) {
                edges.author_time = _strtoi64(gt + 1, nullptr, 10);
            }
        } else if (line_len >= 11 && std::memcmp(data + pos, "committer ", 10) == 0) {
            const char* gt = static_cast<const char*>(std::memchr(data + pos + 10, '>', line_len - 10));
            if (gt != nullptr) {
                edges.committer_time = _strtoi64(gt + 1, nullptr, 10);
            }
        }
        pos = line_end + 1;
    }
    if (edges.committer_time == 0) {
        edges.committer_time = edges.author_time;
    }
}

bool read_loose_commit_edges(char* git_dir, const BtOid& oid, CachedCommitEdges& edges) {
    std::vector<uint8_t> compressed = read_all_bytes(object_path(git_dir, oid));
    if (compressed.empty()) return false;
    std::vector<uint8_t> inflated;
    if (!inflate_zlib_dynamic(compressed, inflated)) return false;
    auto nul = std::find(inflated.begin(), inflated.end(), 0);
    if (nul == inflated.end()) return false;
    std::string header(inflated.begin(), nul);
    if (!starts_with(header, "commit ")) return false;
    parse_commit_edges_from_data(reinterpret_cast<const uint8_t*>(&*(nul + 1)), static_cast<size_t>(inflated.end() - (nul + 1)), edges);
    return true;
}

bool read_pack_commit_edges_at(char* git_dir, const std::string& pack_path, uint64_t offset, CachedCommitEdges& edges, BtCommitGraphCache* cache_handle) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    std::shared_ptr<MappedPack> mapped = get_mapped_pack(cache, pack_path);
    if (!mapped || mapped->size < offset + 1) return false;
    uint8_t* pack = mapped->data;
    size_t pack_size = mapped->size;
    size_t pos = static_cast<size_t>(offset);
    uint8_t c = pack[pos++];
    int type = (c >> 4) & 0x07;
    uint64_t size = c & 0x0f;
    int shift = 4;
    while (c & 0x80) {
        if (pos >= pack_size) return false;
        c = pack[pos++];
        size |= static_cast<uint64_t>(c & 0x7f) << shift;
        shift += 7;
    }
    if (type == 6 || type == 7) {
        GitObject object;
        if (!read_pack_object_at(git_dir, pack_path, offset, object, cache_handle) || object.type != "commit") return false;
        parse_commit_edges_from_data(object.data.data(), object.data.size(), edges);
        return true;
    }
    if (type != 1) return false;
    std::vector<uint8_t> inflated;
    uint64_t next_offset = find_next_pack_offset(cache, pack_path, offset);
    size_t compressed_end = (next_offset > pos && next_offset <= pack_size) ? static_cast<size_t>(next_offset) : pack_size;
    if (!inflate_zlib_dynamic(pack + pos, compressed_end - pos, inflated)) return false;
    parse_commit_edges_from_data(inflated.data(), inflated.size(), edges);
    return true;
}

const CachedCommitEdges* get_commit_edges_cached(char* git_dir, const BtOid& oid, BtCommitGraphCache* cache_handle = nullptr) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    load_commit_graph(git_dir, cache);
    load_persistent_commit_cache(git_dir, cache);
    auto cached = cache.commit_edges.find(oid);
    if (cached != cache.commit_edges.end()) {
        return &cached->second;
    }
    CachedCommitEdges edges;
    bool parsed = read_loose_commit_edges(git_dir, oid, edges);
    if (!parsed) {
        std::string pack_path;
        uint64_t offset = 0;
        if (find_pack_offset(git_dir, oid, pack_path, offset, cache_handle)) {
            parsed = read_pack_commit_edges_at(git_dir, pack_path, offset, edges, cache_handle);
        }
    }
    if (!parsed) return nullptr;
    auto inserted = cache.commit_edges.emplace(oid, std::move(edges));
    cache.persistent_commit_cache_dirty = true;
    return &inserted.first->second;
}

bool parse_commit_edges(char* git_dir, const BtOid& oid, std::vector<BtOid>& parents, int64_t& author_time, BtCommitGraphCache* cache_handle = nullptr) {
    const CachedCommitEdges* edges = get_commit_edges_cached(git_dir, oid, cache_handle);
    if (!edges) return false;
    parents = edges->parents;
    author_time = edges->author_time;
    return true;
}

void preload_pack_commit_edges(char* git_dir, BtCommitGraphCache* cache_handle) {
    RepositoryObjectCache& cache = object_cache(git_dir, cache_handle);
    load_pack_indexes(git_dir, cache);
    load_persistent_commit_cache(git_dir, cache);
}

bool parse_commit_committer_time(char* git_dir, const BtOid& oid, int64_t& committer_time, BtCommitGraphCache* cache_handle = nullptr) {
    const GitObject* object = get_git_object_cached(git_dir, oid, cache_handle);
    if (!object || object->type != "commit") return false;
    const char* data = reinterpret_cast<const char*>(object->data.data());
    size_t len = object->data.size();
    size_t pos = 0;
    int64_t author_time = 0;
    while (pos < len) {
        size_t line_end = pos;
        while (line_end < len && data[line_end] != '\n') line_end++;
        if (line_end == pos) {
            break;
        }
        size_t line_len = line_end - pos;
        if (line_len >= 10 && std::memcmp(data + pos, "committer ", 10) == 0) {
            const char* gt = static_cast<const char*>(std::memchr(data + pos + 10, '>', line_len - 10));
            if (gt != nullptr) {
                committer_time = _strtoi64(gt + 1, nullptr, 10);
                return true;
            }
        } else if (line_len >= 8 && std::memcmp(data + pos, "author ", 7) == 0) {
            const char* gt = static_cast<const char*>(std::memchr(data + pos + 7, '>', line_len - 7));
            if (gt != nullptr) {
                author_time = _strtoi64(gt + 1, nullptr, 10);
            }
        }
        pos = line_end + 1;
    }
    if (author_time != 0) {
        committer_time = author_time;
        return true;
    }
    return false;
}

bool peel_tag_object(char* git_dir, const BtOid& oid, BtOid& peeled_oid) {
    GitObject object;
    if (!read_git_object(git_dir, oid, object, nullptr) || object.type != "tag") {
        peeled_oid = oid;
        return true;
    }
    const char* data = reinterpret_cast<const char*>(object.data.data());
    size_t len = object.data.size();
    size_t pos = 0;
    while (pos < len) {
        size_t line_end = pos;
        while (line_end < len && data[line_end] != '\n') line_end++;
        if (line_end == pos) break;
        size_t line_len = line_end - pos;
        if (line_len >= 47 && std::memcmp(data + pos, "object ", 7) == 0) {
            return parse_hex_oid_at(data + pos + 7, line_len - 7, peeled_oid);
        }
        pos = line_end + 1;
    }
    peeled_oid = oid;
    return true;
}

bool oid_equal(const BtOid& a, const BtOid& b) {
    return a.s0 == b.s0 && a.s1 == b.s1 && a.s2 == b.s2 && a.s3 == b.s3 && a.s4 == b.s4;
}

bool oid_less(const BtOid& a, const BtOid& b) {
    if (a.s0 != b.s0) return a.s0 < b.s0;
    if (a.s1 != b.s1) return a.s1 < b.s1;
    if (a.s2 != b.s2) return a.s2 < b.s2;
    if (a.s3 != b.s3) return a.s3 < b.s3;
    return a.s4 < b.s4;
}

bool contains_oid(const std::vector<BtOid>& values, const BtOid& oid) {
    return std::any_of(values.begin(), values.end(), [&](const BtOid& item) { return oid_equal(item, oid); });
}

std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>> get_reachable_commits(char* git_dir_path, const BtOid& start, BtCommitGraphCache* cache_handle) {
    RepositoryObjectCache& cache = object_cache(git_dir_path, cache_handle);
    auto cached = cache.reachable_commits.find(start);
    if (cached != cache.reachable_commits.end()) {
        return cached->second;
    }
    auto reachable = std::make_shared<std::unordered_set<BtOid, BtOidHash, BtOidEqual>>();
    std::vector<BtOid> stack;
    stack.push_back(start);
    while (!stack.empty()) {
        BtOid oid = stack.back();
        stack.pop_back();
        if (!reachable->insert(oid).second) {
            continue;
        }
        std::vector<BtOid> parents;
        int64_t author_time = 0;
        if (!parse_commit_edges(git_dir_path, oid, parents, author_time, cache_handle)) {
            return nullptr;
        }
        for (const BtOid& parent : parents) {
            if (reachable->find(parent) == reachable->end()) {
                stack.push_back(parent);
            }
        }
    }
    cache.reachable_commits[start] = reachable;
    return reachable;
}

bool build_commit_storage_native(char* git_dir_path, const std::vector<BtOid>& tips, bool date_order, int64_t skip_count, int64_t max_count, BtCommitGraphCache* cache_handle, BtCancellationToken* cancellation_token_ptr, std::vector<BtOid>& flat, std::vector<uint32_t>& indexes, bool& hit_limit) {
    preload_pack_commit_edges(git_dir_path, cache_handle);
    std::vector<BtOid> stack = tips;
	std::unordered_set<BtOid, BtOidHash, BtOidEqual> seen;
    seen.reserve(4096);
    std::vector<CommitRecord> records;
    records.reserve(4096);
    hit_limit = false;
    while (!stack.empty()) {
        if (cancellation_token_ptr && cancellation_token_ptr->inner && static_cast<std::atomic_bool*>(cancellation_token_ptr->inner)->load()) {
            set_error("Canceled");
            return false;
        }
        BtOid oid = stack.back();
        stack.pop_back();
		if (seen.find(oid) != seen.end()) continue;
        std::vector<BtOid> parents;
        int64_t author_time = 0;
        if (!parse_commit_edges(git_dir_path, oid, parents, author_time, cache_handle)) {
            continue;
        }
		seen.insert(oid);
        records.push_back(CommitRecord{ oid, parents, author_time });
        for (auto it = parents.rbegin(); it != parents.rend(); ++it) {
			if (seen.find(*it) == seen.end()) {
                stack.push_back(*it);
            }
        }
        if (!date_order && skip_count == 0 && max_count > 0 && static_cast<int64_t>(records.size()) >= max_count) {
            hit_limit = true;
            break;
        }
    }
    size_t start = static_cast<size_t>(std::max<int64_t>(0, skip_count));
    size_t end = records.size();
    if (max_count > 0 && start < end) {
        end = (std::min)(end, start + static_cast<size_t>(max_count));
    }
    hit_limit = end < records.size() || (max_count > 0 && end >= start && end - start >= static_cast<size_t>(max_count));
    for (size_t i = start; i < end; ++i) {
        indexes.push_back(static_cast<uint32_t>(flat.size()));
        flat.push_back(records[i].oid);
        for (const BtOid& parent : records[i].parents) {
            flat.push_back(parent);
        }
    }
    return true;
}

bool build_commit_storage_priority_native(char* git_dir_path, const std::vector<BtOid>& tips, int64_t skip_count, int64_t max_count, BtCommitGraphCache* cache_handle, BtCancellationToken* cancellation_token_ptr, std::vector<BtOid>& flat, std::vector<uint32_t>& indexes, bool& hit_limit) {
    preload_pack_commit_edges(git_dir_path, cache_handle);
    std::unordered_map<BtOid, size_t, BtOidHash, BtOidEqual> record_indexes;
    std::vector<CommitRecord> records;
    std::vector<BtOid> stack = tips;
    hit_limit = false;

    while (!stack.empty()) {
        if (cancellation_token_ptr && cancellation_token_ptr->inner && static_cast<std::atomic_bool*>(cancellation_token_ptr->inner)->load()) {
            set_error("Canceled");
            return false;
        }
        BtOid oid = stack.back();
        stack.pop_back();
        if (record_indexes.find(oid) != record_indexes.end()) {
            continue;
        }
        std::vector<BtOid> parents;
        int64_t author_time = 0;
        if (!parse_commit_edges(git_dir_path, oid, parents, author_time, cache_handle)) {
            continue;
        }
        record_indexes[oid] = records.size();
        records.push_back(CommitRecord{ oid, parents, author_time });
        for (auto it = parents.rbegin(); it != parents.rend(); ++it) {
            if (record_indexes.find(*it) == record_indexes.end()) {
                stack.push_back(*it);
            }
        }
    }

    std::vector<uint32_t> remaining_children(records.size(), 0);
    for (const CommitRecord& record : records) {
        for (const BtOid& parent : record.parents) {
            auto found = record_indexes.find(parent);
            if (found != record_indexes.end()) {
                ++remaining_children[found->second];
            }
        }
    }

    struct ReadyItem {
        size_t index;
        int64_t priority;
        int64_t time;
        BtOid oid;
    };
    struct ReadyCompare {
        bool operator()(const ReadyItem& lhs, const ReadyItem& rhs) const {
            if (lhs.priority != rhs.priority) return lhs.priority < rhs.priority;
            if (lhs.time != rhs.time) return lhs.time < rhs.time;
            return oid_less(rhs.oid, lhs.oid);
        }
    };
    std::priority_queue<ReadyItem, std::vector<ReadyItem>, ReadyCompare> ready;
    std::vector<uint8_t> emitted(records.size(), 0);
    std::vector<int64_t> continuation_priority(records.size(), 0);
    for (size_t i = 0; i < records.size(); ++i) {
        if (remaining_children[i] == 0) {
            ready.push(ReadyItem{ i, 0, records[i].time, records[i].oid });
        }
    }

    size_t emitted_count = 0;
    size_t start = static_cast<size_t>(std::max<int64_t>(0, skip_count));
    size_t end_limit = max_count > 0 ? start + static_cast<size_t>(max_count) : static_cast<size_t>(-1);
    while (!ready.empty()) {
        if (cancellation_token_ptr && cancellation_token_ptr->inner && static_cast<std::atomic_bool*>(cancellation_token_ptr->inner)->load()) {
            set_error("Canceled");
            return false;
        }

        size_t record_index = ready.top().index;
        ready.pop();
        if (emitted[record_index]) {
            continue;
        }
        emitted[record_index] = 1;

        const CommitRecord& record = records[record_index];
        if (emitted_count >= start && emitted_count < end_limit) {
            indexes.push_back(static_cast<uint32_t>(flat.size()));
            flat.push_back(record.oid);
            for (const BtOid& parent : record.parents) {
                flat.push_back(parent);
            }
        }
        ++emitted_count;
        if (emitted_count > end_limit) {
            hit_limit = true;
            break;
        }

        int parent_position = 0;
        for (const BtOid& parent : record.parents) {
            auto found = record_indexes.find(parent);
            if (found == record_indexes.end()) {
                ++parent_position;
                continue;
            }
            size_t parent_index = found->second;
            if (remaining_children[parent_index] > 0) {
                --remaining_children[parent_index];
                if (remaining_children[parent_index] == 0) {
                    continuation_priority[parent_index] = static_cast<int64_t>(records.size() - emitted_count) * 16 - parent_position;
                    ready.push(ReadyItem{ parent_index, continuation_priority[parent_index], records[parent_index].time, records[parent_index].oid });
                }
            }
            ++parent_position;
        }
    }
    hit_limit = hit_limit || emitted_count < records.size();
    return true;
}

template <typename T>
void assign_vector(void*& ptr, int64_t& len, int64_t& cap, const std::vector<T>& values) {
    ptr = dup_array(values);
    len = cap = static_cast<int64_t>(values.size());
}

void assign_bytes(void*& ptr, int64_t& len, int64_t& cap, const std::string& bytes) {
    if (bytes.empty()) {
        ptr = nullptr;
        len = cap = 0;
        return;
    }
    char* data = static_cast<char*>(std::malloc(bytes.size()));
    std::memcpy(data, bytes.data(), bytes.size());
    ptr = data;
    len = cap = static_cast<int64_t>(bytes.size());
}

uint64_t fnv1a(const std::string& bytes) {
    uint64_t hash = 1469598103934665603ull;
    for (unsigned char ch : bytes) {
        hash ^= ch;
        hash *= 1099511628211ull;
    }
    return hash;
}

uint32_t crc32(const uint8_t* data, size_t len) {
    uint32_t crc = 0xffffffffu;
    for (size_t i = 0; i < len; ++i) {
        crc ^= data[i];
        for (int j = 0; j < 8; ++j) {
            crc = (crc >> 1) ^ (0xedb88320u & (0u - (crc & 1u)));
        }
    }
    return crc ^ 0xffffffffu;
}

uint32_t adler32(const uint8_t* data, size_t len) {
    uint32_t a = 1, b = 0;
    for (size_t i = 0; i < len; ++i) {
        a = (a + data[i]) % 65521u;
        b = (b + a) % 65521u;
    }
    return (b << 16) | a;
}

void append_be32(std::vector<uint8_t>& out, uint32_t value) {
    out.push_back(static_cast<uint8_t>((value >> 24) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 16) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
    out.push_back(static_cast<uint8_t>(value & 0xff));
}

void append_chunk(std::vector<uint8_t>& png, const char* type, const std::vector<uint8_t>& data) {
    append_be32(png, static_cast<uint32_t>(data.size()));
    size_t type_pos = png.size();
    png.insert(png.end(), type, type + 4);
    png.insert(png.end(), data.begin(), data.end());
    uint32_t crc = crc32(png.data() + type_pos, 4 + data.size());
    append_be32(png, crc);
}

std::vector<uint8_t> zlib_store(const std::vector<uint8_t>& raw) {
    std::vector<uint8_t> out;
    out.push_back(0x78);
    out.push_back(0x01);
    size_t pos = 0;
    while (pos < raw.size()) {
        uint16_t block_len = static_cast<uint16_t>(std::min<size_t>(65535, raw.size() - pos));
        bool final = pos + block_len >= raw.size();
        out.push_back(final ? 0x01 : 0x00);
        out.push_back(static_cast<uint8_t>(block_len & 0xff));
        out.push_back(static_cast<uint8_t>((block_len >> 8) & 0xff));
        uint16_t nlen = static_cast<uint16_t>(~block_len);
        out.push_back(static_cast<uint8_t>(nlen & 0xff));
        out.push_back(static_cast<uint8_t>((nlen >> 8) & 0xff));
        out.insert(out.end(), raw.begin() + pos, raw.begin() + pos + block_len);
        pos += block_len;
    }
    append_be32(out, adler32(raw.data(), raw.size()));
    return out;
}

void append_le16(std::vector<uint8_t>& out, uint16_t value) {
    out.push_back(static_cast<uint8_t>(value & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
}

void append_le32(std::vector<uint8_t>& out, uint32_t value) {
    out.push_back(static_cast<uint8_t>(value & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 8) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 16) & 0xff));
    out.push_back(static_cast<uint8_t>((value >> 24) & 0xff));
}

bool decode_tga_to_bmp(uint8_t* data, int64_t len, std::vector<uint8_t>& bmp) {
    if (!data || len < 18) return false;
    uint8_t id_len = data[0];
    uint8_t color_map_type = data[1];
    uint8_t image_type = data[2];
    if (color_map_type != 0 || (image_type != 2 && image_type != 3 && image_type != 10 && image_type != 11)) return false;
    uint16_t width = static_cast<uint16_t>(data[12] | (data[13] << 8));
    uint16_t height = static_cast<uint16_t>(data[14] | (data[15] << 8));
    uint8_t bpp = data[16];
    bool grayscale = image_type == 3 || image_type == 11;
    if (width == 0 || height == 0 || (!grayscale && bpp != 24 && bpp != 32) || (grayscale && bpp != 8)) return false;
    size_t bytes_per_pixel = grayscale ? 1 : bpp / 8;
    size_t pixel_offset = 18 + id_len;
    size_t pixel_count = static_cast<size_t>(width) * height;
    size_t pixel_bytes = pixel_count * bytes_per_pixel;
    if (image_type == 2 && pixel_offset + pixel_bytes > static_cast<size_t>(len)) return false;
    std::vector<uint8_t> decoded_pixels;
    const uint8_t* pixel_data = data + pixel_offset;
    if (image_type == 10 || image_type == 11) {
        decoded_pixels.reserve(pixel_bytes);
        size_t pos = pixel_offset;
        while (decoded_pixels.size() < pixel_bytes && pos < static_cast<size_t>(len)) {
            uint8_t header = data[pos++];
            size_t count = static_cast<size_t>((header & 0x7f) + 1);
            if ((header & 0x80) != 0) {
                if (pos + bytes_per_pixel > static_cast<size_t>(len)) return false;
                for (size_t i = 0; i < count && decoded_pixels.size() < pixel_bytes; ++i) {
                    decoded_pixels.insert(decoded_pixels.end(), data + pos, data + pos + bytes_per_pixel);
                }
                pos += bytes_per_pixel;
            } else {
                size_t bytes = count * bytes_per_pixel;
                if (pos + bytes > static_cast<size_t>(len)) return false;
                decoded_pixels.insert(decoded_pixels.end(), data + pos, data + pos + bytes);
                pos += bytes;
            }
        }
        if (decoded_pixels.size() < pixel_bytes) return false;
        pixel_data = decoded_pixels.data();
    }
    bool top_origin = (data[17] & 0x20) != 0;
    uint32_t row_stride = ((static_cast<uint32_t>(width) * 3u + 3u) / 4u) * 4u;
    uint32_t pixel_data_size = row_stride * height;
    uint32_t file_size = 14u + 40u + pixel_data_size;
    bmp.clear();
    bmp.reserve(file_size);
    bmp.push_back('B');
    bmp.push_back('M');
    append_le32(bmp, file_size);
    append_le16(bmp, 0);
    append_le16(bmp, 0);
    append_le32(bmp, 54);
    append_le32(bmp, 40);
    append_le32(bmp, width);
    append_le32(bmp, height);
    append_le16(bmp, 1);
    append_le16(bmp, 24);
    append_le32(bmp, 0);
    append_le32(bmp, pixel_data_size);
    append_le32(bmp, 0);
    append_le32(bmp, 0);
    append_le32(bmp, 0);
    append_le32(bmp, 0);
    for (uint16_t y = 0; y < height; ++y) {
        uint16_t src_y = top_origin ? static_cast<uint16_t>(height - 1 - y) : y;
        size_t row_start = bmp.size();
        for (uint16_t x = 0; x < width; ++x) {
            size_t p = (static_cast<size_t>(src_y) * width + x) * bytes_per_pixel;
            if (grayscale) {
                bmp.push_back(pixel_data[p]);
                bmp.push_back(pixel_data[p]);
                bmp.push_back(pixel_data[p]);
            } else {
                bmp.push_back(pixel_data[p]);
                bmp.push_back(pixel_data[p + 1]);
                bmp.push_back(pixel_data[p + 2]);
            }
        }
        while (bmp.size() - row_start < row_stride) {
            bmp.push_back(0);
        }
    }
    return true;
}

bool is_common_encoded_image(uint8_t* data, int64_t len) {
    if (!data || len < 4) return false;
    if (len >= 8 && data[0] == 0x89 && data[1] == 'P' && data[2] == 'N' && data[3] == 'G' && data[4] == 0x0d && data[5] == 0x0a && data[6] == 0x1a && data[7] == 0x0a) return true;
    if (data[0] == 'B' && data[1] == 'M') return true;
    if (data[0] == 0xff && data[1] == 0xd8 && data[2] == 0xff) return true;
    if (len >= 6 && std::memcmp(data, "GIF87a", 6) == 0) return true;
    if (len >= 6 && std::memcmp(data, "GIF89a", 6) == 0) return true;
    if (len >= 12 && std::memcmp(data, "RIFF", 4) == 0 && std::memcmp(data + 8, "WEBP", 4) == 0) return true;
    return false;
}

void add_token(std::vector<BtPatchToken>& tokens, uint8_t kind, size_t start, size_t end) {
    tokens.push_back(BtPatchToken{ kind, static_cast<uint32_t>(start), static_cast<uint32_t>(end) });
}

bool starts_with(const std::string& value, const char* prefix) {
    return value.rfind(prefix, 0) == 0;
}

void tokenize_chunk_header(const std::string& line, size_t line_start, size_t line_token_end, std::vector<BtPatchToken>& tokens) {
    add_token(tokens, 16, line_start, line_token_end);
    size_t minus = line.find('-');
    size_t plus = line.find('+');
    if (minus != std::string::npos) {
        size_t p = minus + 1;
        size_t start = p;
        while (p < line.size() && std::isdigit(static_cast<unsigned char>(line[p]))) p++;
        add_token(tokens, 17, line_start + start, line_start + p);
        if (p < line.size() && line[p] == ',') {
            size_t len_start = ++p;
            while (p < line.size() && std::isdigit(static_cast<unsigned char>(line[p]))) p++;
            add_token(tokens, 18, line_start + len_start, line_start + p);
        }
    }
    if (plus != std::string::npos) {
        size_t p = plus + 1;
        size_t start = p;
        while (p < line.size() && std::isdigit(static_cast<unsigned char>(line[p]))) p++;
        add_token(tokens, 19, line_start + start, line_start + p);
        if (p < line.size() && line[p] == ',') {
            size_t len_start = ++p;
            while (p < line.size() && std::isdigit(static_cast<unsigned char>(line[p]))) p++;
            add_token(tokens, 20, line_start + len_start, line_start + p);
        }
    }
    size_t ctx = line.rfind("@@");
    if (ctx != std::string::npos && ctx + 2 < line.size()) {
        size_t begin = ctx + 2;
        while (begin < line.size() && line[begin] == ' ') begin++;
        if (begin < line.size()) add_token(tokens, 21, line_start + begin, line_token_end);
    }
}

std::string bytes_to_string(uint8_t* data, uint64_t len) {
    if (!data || len == 0) return "";
    return std::string(reinterpret_cast<char*>(data), reinterpret_cast<char*>(data) + len);
}

bool add_diff_header_path_tokens(const std::string& line, size_t line_start, const std::string& src_prefix, const std::string& dst_prefix, std::vector<BtPatchToken>& tokens) {
    std::string src_marker = " " + src_prefix;
    std::string dst_marker = " " + dst_prefix;
    size_t src = line.find(src_marker);
    size_t dst = line.find(dst_marker, src == std::string::npos ? 0 : src + src_marker.size());
    if (src != std::string::npos && dst != std::string::npos) {
        add_token(tokens, 1, line_start + src + src_marker.size(), line_start + dst);
        add_token(tokens, 2, line_start + dst + dst_marker.size(), line_start + line.size());
        return true;
    }
    src = line.find(" a/");
    dst = line.find(" b/", src == std::string::npos ? 0 : src + 3);
    if (src != std::string::npos && dst != std::string::npos) {
        add_token(tokens, 1, line_start + src + 3, line_start + dst);
        add_token(tokens, 2, line_start + dst + 3, line_start + line.size());
        return true;
    }
    size_t first_quote = line.find('"');
    if (first_quote != std::string::npos) {
        size_t second_quote = line.find('"', first_quote + 1);
        size_t third_quote = second_quote == std::string::npos ? std::string::npos : line.find('"', second_quote + 1);
        size_t fourth_quote = third_quote == std::string::npos ? std::string::npos : line.find('"', third_quote + 1);
        if (second_quote != std::string::npos && third_quote != std::string::npos && fourth_quote != std::string::npos) {
            size_t src_start = first_quote + 1;
            size_t dst_start = third_quote + 1;
            if (line.compare(src_start, src_prefix.size(), src_prefix) == 0) src_start += src_prefix.size();
            else if (line.compare(src_start, 2, "a/") == 0) src_start += 2;
            if (line.compare(dst_start, dst_prefix.size(), dst_prefix) == 0) dst_start += dst_prefix.size();
            else if (line.compare(dst_start, 2, "b/") == 0) dst_start += 2;
            add_token(tokens, 1, line_start + src_start, line_start + second_quote);
            add_token(tokens, 2, line_start + dst_start, line_start + fourth_quote);
            return true;
        }
    }
    return false;
}

BtResult run_process(char* path, char* current_dir, char** args, int64_t args_len, uint8_t* stdin_ptr, int64_t stdin_len, std::string& out_stdout, std::string& out_stderr, int& status) {
    SECURITY_ATTRIBUTES sa{ sizeof(SECURITY_ATTRIBUTES), nullptr, TRUE };
    HANDLE stdout_read = nullptr, stdout_write = nullptr;
    HANDLE stderr_read = nullptr, stderr_write = nullptr;
    HANDLE stdin_read = nullptr, stdin_write = nullptr;
    if (!CreatePipe(&stdout_read, &stdout_write, &sa, 0) || !CreatePipe(&stderr_read, &stderr_write, &sa, 0) || !CreatePipe(&stdin_read, &stdin_write, &sa, 0)) {
        set_error("CreatePipe failed");
        return Err;
    }
    SetHandleInformation(stdout_read, HANDLE_FLAG_INHERIT, 0);
    SetHandleInformation(stderr_read, HANDLE_FLAG_INHERIT, 0);
    SetHandleInformation(stdin_write, HANDLE_FLAG_INHERIT, 0);

    STARTUPINFOW si{};
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;
    si.hStdOutput = stdout_write;
    si.hStdError = stderr_write;
    si.hStdInput = stdin_read;
    PROCESS_INFORMATION pi{};
    std::wstring command = build_command(path, args, args_len);
    std::wstring cwd = utf8_to_wide(current_dir);
    BOOL ok = CreateProcessW(nullptr, command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW, nullptr, cwd.empty() ? nullptr : cwd.c_str(), &si, &pi);
    CloseHandle(stdout_write);
    CloseHandle(stderr_write);
    CloseHandle(stdin_read);
    if (!ok) {
        CloseHandle(stdout_read);
        CloseHandle(stderr_read);
        CloseHandle(stdin_write);
        set_error("CreateProcess failed");
        return Err;
    }
    if (stdin_ptr && stdin_len > 0) {
        DWORD written = 0;
        WriteFile(stdin_write, stdin_ptr, static_cast<DWORD>(stdin_len), &written, nullptr);
    }
    CloseHandle(stdin_write);

    auto reader = [](HANDLE handle, std::string* output) {
        char buffer[4096];
        DWORD read = 0;
        while (ReadFile(handle, buffer, sizeof(buffer), &read, nullptr) && read > 0) {
            output->append(buffer, buffer + read);
        }
        CloseHandle(handle);
    };
    std::thread stdout_thread(reader, stdout_read, &out_stdout);
    std::thread stderr_thread(reader, stderr_read, &out_stderr);
    WaitForSingleObject(pi.hProcess, INFINITE);
    DWORD exit_code = 0;
    GetExitCodeProcess(pi.hProcess, &exit_code);
    stdout_thread.join();
    stderr_thread.join();
    CloseHandle(pi.hThread);
    CloseHandle(pi.hProcess);
    status = static_cast<int>(exit_code);
    return Ok;
}

}

extern "C" {

__declspec(dllexport) BtResult __cdecl bt_oid_from_str(uint8_t* sha_string, BtOid* out_result) {
    if (!out_result || !parse_hex_oid(reinterpret_cast<const char*>(sha_string), *out_result)) {
        set_error("Invalid SHA");
        return Err;
    }
    return Ok;
}

__declspec(dllexport) int64_t __cdecl bt_get_last_error_message(char* buffer, uint64_t length) {
    uint64_t required = static_cast<uint64_t>(g_last_error.size() + 1);
    if (!buffer || length < required) return -static_cast<int64_t>(required);
    std::memcpy(buffer, g_last_error.c_str(), required);
    return static_cast<int64_t>(required);
}

__declspec(dllexport) BtResult bt_decode_image(uint8_t* image_data_ptr, int64_t image_data_len, BtDecodeImageResult* out_result) {
    if (!out_result || image_data_len < 0) return Err;
    std::vector<uint8_t> decoded;
    if (is_common_encoded_image(image_data_ptr, image_data_len)) {
        decoded.assign(image_data_ptr, image_data_ptr + image_data_len);
    } else if (!decode_tga_to_bmp(image_data_ptr, image_data_len, decoded)) {
        set_error("Unsupported image format");
        return Err;
    }
    out_result->data = static_cast<uint8_t*>(std::malloc(decoded.size()));
    if (!decoded.empty() && out_result->data) std::memcpy(out_result->data, decoded.data(), decoded.size());
    out_result->data_len = static_cast<int64_t>(decoded.size());
    out_result->data_cap = static_cast<int64_t>(decoded.size());
    return Ok;
}

__declspec(dllexport) void bt_release_decode_image(BtDecodeImageResult* r) { if (r) { void* p = r->data; release_ptr(p); r->data = nullptr; r->data_len = r->data_cap = 0; } }

__declspec(dllexport) BtResult __cdecl bt_md_to_html(char* md_utf8, BtMdToHtmlResult* out_result) {
    if (!out_result) return Err;
    out_result->html = dup_string(markdown_to_html(md_utf8));
    return out_result->html ? Ok : Err;
}

__declspec(dllexport) void __cdecl bt_release_md_to_html(BtMdToHtmlResult* r) { if (r) release_char(r->html); }

__declspec(dllexport) BtResult __cdecl bt_parse_patch(uint8_t* patch_utf8, uint64_t patch_utf8_len, uint8_t* src_prefix_utf8, uint64_t src_prefix_utf8_len, uint8_t* dst_prefix_utf8, uint64_t dst_prefix_utf8_len, BtParsePatchResult* out_result) {
    if (!out_result || !patch_utf8) return Err;
    std::string patch(reinterpret_cast<char*>(patch_utf8), reinterpret_cast<char*>(patch_utf8) + patch_utf8_len);
    std::string src_prefix = bytes_to_string(src_prefix_utf8, src_prefix_utf8_len);
    std::string dst_prefix = bytes_to_string(dst_prefix_utf8, dst_prefix_utf8_len);
    std::vector<BtPatchToken> tokens;
    size_t pos = 0;
    while (pos < patch.size()) {
        size_t line_end = patch.find('\n', pos);
        if (line_end == std::string::npos) line_end = patch.size();
        size_t content_end = (line_end > pos && patch[line_end - 1] == '\r') ? line_end - 1 : line_end;
        size_t token_end = line_end + (line_end < patch.size() ? 1 : 0);
        std::string line = patch.substr(pos, content_end - pos);
        if (starts_with(line, "diff --git ")) {
            add_token(tokens, 0, pos + 7, pos + 10);
            add_diff_header_path_tokens(line, pos, src_prefix, dst_prefix, tokens);
        } else if (starts_with(line, "index ")) {
            size_t first = 6, dots = line.find("..", first), space = line.find(' ', dots == std::string::npos ? first : dots);
            if (dots != std::string::npos) {
                add_token(tokens, 3, pos + first, pos + dots);
                add_token(tokens, 4, pos + dots + 2, pos + (space == std::string::npos ? line.size() : space));
                if (space != std::string::npos) add_token(tokens, 5, pos + space + 1, content_end);
            }
        } else if (starts_with(line, "similarity index ")) add_token(tokens, 6, pos + 17, content_end);
        else if (starts_with(line, "copy from ")) add_token(tokens, 7, pos + 10, content_end);
        else if (starts_with(line, "copy to ")) add_token(tokens, 8, pos + 8, content_end);
        else if (starts_with(line, "rename from ")) add_token(tokens, 9, pos + 12, content_end);
        else if (starts_with(line, "rename to ")) add_token(tokens, 10, pos + 10, content_end);
        else if (starts_with(line, "deleted file mode ")) add_token(tokens, 11, pos + 18, content_end);
        else if (starts_with(line, "new file mode ")) add_token(tokens, 12, pos + 14, content_end);
        else if (starts_with(line, "old mode ")) add_token(tokens, 13, pos + 9, content_end);
        else if (starts_with(line, "new mode ")) add_token(tokens, 14, pos + 9, content_end);
        else if (starts_with(line, "Binary files ")) add_token(tokens, 15, pos, token_end);
        else if (starts_with(line, "GIT binary patch")) add_token(tokens, 15, pos, token_end);
        else if (starts_with(line, "@@ ")) tokenize_chunk_header(line, pos, token_end, tokens);
        else if (!line.empty() && line[0] == ' ') add_token(tokens, 22, pos + 1, token_end);
        else if (!line.empty() && line[0] == '+') add_token(tokens, 23, pos + 1, token_end);
        else if (!line.empty() && line[0] == '-') add_token(tokens, 24, pos + 1, token_end);
        else if (starts_with(line, "\\ ")) add_token(tokens, 25, pos + 2, token_end);
        else add_token(tokens, 26, pos, token_end);
        pos = line_end + (line_end < patch.size() ? 1 : 0);
    }
    out_result->tokens = dup_array(tokens);
    out_result->tokens_len = static_cast<int64_t>(tokens.size());
    out_result->tokens_cap = static_cast<int64_t>(tokens.size());
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_parse_patch(BtParsePatchResult* r) { if (r) { release_ptr(r->tokens); r->tokens_len = r->tokens_cap = 0; } }

__declspec(dllexport) BtResult __cdecl bt_layout_treemap(int64_t* sizes_ptr, int64_t sizes_len, BtRect rect, BtLayoutTreemapResult* out_result) {
    if (!out_result || sizes_len < 0) return Err;
    struct Node { int64_t index; int64_t size; };
    std::vector<Node> nodes;
    for (int64_t i = 0; i < sizes_len; ++i) nodes.push_back(Node{ i, std::max<int64_t>(0, sizes_ptr[i]) });
    std::sort(nodes.begin(), nodes.end(), [](const Node& a, const Node& b) { return a.size > b.size; });
    std::vector<BtTreemapItem> items;
    std::vector<Node> positive_nodes;
    std::vector<Node> zero_nodes;
    for (const Node& node : nodes) {
        if (node.size > 0) positive_nodes.push_back(node);
        else zero_nodes.push_back(node);
    }
    if (!zero_nodes.empty()) {
        size_t layout_count = positive_nodes.size() > 2 ? positive_nodes.size() - 1 : positive_nodes.size();
        int64_t total = 0;
        for (size_t i = 0; i < layout_count; ++i) total += positive_nodes[i].size;
        double y = rect.y;
        for (size_t i = 0; i < layout_count; ++i) {
            double h = total > 0 ? rect.h * static_cast<double>(positive_nodes[i].size) / total : 0.0;
            items.push_back(BtTreemapItem{ positive_nodes[i].index, BtRect{ rect.x, y, rect.w, h } });
            y += h;
        }
        for (size_t i = layout_count; i < positive_nodes.size(); ++i) {
            items.push_back(BtTreemapItem{ positive_nodes[i].index, BtRect{ rect.x, rect.y, 1.0, 1.0 } });
        }
        for (const Node& node : zero_nodes) {
            items.push_back(BtTreemapItem{ node.index, BtRect{ rect.x, rect.y, 1.0, 1.0 } });
        }
        out_result->items = dup_array(items);
        out_result->items_len = out_result->items_cap = static_cast<int64_t>(items.size());
        return Ok;
    }
    if (nodes.size() >= 4 && nodes.back().size > 0 && nodes.front().size >= nodes.back().size * 10) {
        double effective_total = static_cast<double>(nodes.front().size + static_cast<int64_t>(nodes.size()) - 2);
        double first_h = effective_total > 0 ? rect.h * static_cast<double>(nodes.front().size) / effective_total : rect.h;
        items.push_back(BtTreemapItem{ nodes.front().index, BtRect{ rect.x, rect.y, rect.w, first_h } });
        double rest_h = rect.h - first_h;
        double rest_y = rect.y + first_h;
        double x = rect.x;
        double rest_weight = static_cast<double>(nodes.size());
        for (size_t i = 1; i < nodes.size(); ++i) {
            double weight = (i == 1) ? 2.0 : 1.0;
            double w = rect.w * weight / rest_weight;
            items.push_back(BtTreemapItem{ nodes[i].index, BtRect{ x, rest_y, w, rest_h } });
            x += w;
        }
        out_result->items = dup_array(items);
        out_result->items_len = out_result->items_cap = static_cast<int64_t>(items.size());
        return Ok;
    }
    if (nodes.size() > 8) {
        double total = 0.0;
        for (const Node& node : nodes) total += static_cast<double>(node.size);
        std::vector<double> areas;
        areas.reserve(nodes.size());
        double rect_area = (std::max)(0.0, rect.w * rect.h);
        for (const Node& node : nodes) areas.push_back(total > 0.0 ? rect_area * static_cast<double>(node.size) / total : 0.0);
        auto worst = [&](const std::vector<size_t>& row, double side) {
            if (row.empty() || side <= 0.0) return DBL_MAX;
            double sum = 0.0, min_area = DBL_MAX, max_area = 0.0;
            for (size_t idx : row) {
                double area = (std::max)(0.0, areas[idx]);
                sum += area;
                min_area = (std::min)(min_area, area);
                max_area = (std::max)(max_area, area);
            }
            if (sum <= 0.0 || min_area <= 0.0) return DBL_MAX;
            double side2 = side * side;
            return (std::max)(side2 * max_area / (sum * sum), (sum * sum) / (side2 * min_area));
        };
        auto layout_row = [&](const std::vector<size_t>& row, BtRect& r) {
            double sum = 0.0;
            for (size_t idx : row) sum += areas[idx];
            if (sum <= 0.0) return;
            if (r.w >= r.h) {
                double h = r.w > 0.0 ? sum / r.w : 0.0;
                double x = r.x;
                for (size_t idx : row) {
                    double w = h > 0.0 ? areas[idx] / h : 0.0;
                    items.push_back(BtTreemapItem{ nodes[idx].index, BtRect{ x, r.y, w, h } });
                    x += w;
                }
                r.y += h;
                r.h -= h;
            } else {
                double w = r.h > 0.0 ? sum / r.h : 0.0;
                double y = r.y;
                for (size_t idx : row) {
                    double h = w > 0.0 ? areas[idx] / w : 0.0;
                    items.push_back(BtTreemapItem{ nodes[idx].index, BtRect{ r.x, y, w, h } });
                    y += h;
                }
                r.x += w;
                r.w -= w;
            }
        };
        BtRect remaining = rect;
        std::vector<size_t> row;
        for (size_t i = 0; i < nodes.size(); ++i) {
            std::vector<size_t> candidate = row;
            candidate.push_back(i);
            double side = (std::min)(remaining.w, remaining.h);
            if (!row.empty() && worst(candidate, side) > worst(row, side)) {
                layout_row(row, remaining);
                row.clear();
            }
            row.push_back(i);
        }
        layout_row(row, remaining);
        out_result->items = dup_array(items);
        out_result->items_len = out_result->items_cap = static_cast<int64_t>(items.size());
        return Ok;
    }
    auto layout = [&](auto&& self, size_t start, size_t end, BtRect r) -> void {
        if (start >= end) return;
        if (start + 1 == end) {
            items.push_back(BtTreemapItem{ nodes[start].index, r });
            return;
        }
        double denom = static_cast<double>(nodes[start].size + nodes[start + 1].size);
        double fraction = denom > 0 ? nodes[start].size / denom : 0.5;
        BtRect first = r;
        BtRect rest = r;
        if (r.w >= r.h) {
            first.w = r.w * fraction;
            rest.x = r.x + first.w;
            rest.w = r.w - first.w;
        } else {
            first.h = r.h * fraction;
            rest.y = r.y + first.h;
            rest.h = r.h - first.h;
        }
        items.push_back(BtTreemapItem{ nodes[start].index, first });
        self(self, start + 1, end, rest);
    };
    layout(layout, 0, nodes.size(), rect);
    std::sort(items.begin(), items.end(), [](const BtTreemapItem& a, const BtTreemapItem& b) {
        if (a.rect.x != b.rect.x) return a.rect.x < b.rect.x;
        if (a.rect.y != b.rect.y) return a.rect.y < b.rect.y;
        return a.index < b.index;
    });
    out_result->items = dup_array(items);
    out_result->items_len = out_result->items_cap = static_cast<int64_t>(items.size());
    return Ok;
}
__declspec(dllexport) void __cdecl bt_release_layout_treemap(BtLayoutTreemapResult* r) { if (r) { release_ptr(r->items); r->items_len = r->items_cap = 0; } }

bool is_identifier_start_char(char ch) {
    return std::isalpha(static_cast<unsigned char>(ch)) || ch == '_';
}

bool is_identifier_char(char ch) {
    return std::isalnum(static_cast<unsigned char>(ch)) || ch == '_';
}

bool has_supported_highlight_extension(const std::string& path) {
    std::string lower = path;
    std::transform(lower.begin(), lower.end(), lower.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    const char* extensions[] = { ".cs", ".cpp", ".cc", ".cxx", ".c", ".h", ".hpp", ".java", ".js", ".jsx", ".ts", ".tsx", ".json", ".css", ".xml", ".xaml", ".html", ".htm", ".py", ".sh", ".ps1", ".go", ".rs" };
    for (const char* extension : extensions) {
        if (lower.size() >= std::strlen(extension) && lower.compare(lower.size() - std::strlen(extension), std::strlen(extension), extension) == 0) {
            return true;
        }
    }
    return false;
}

bool range_overlaps(uint32_t start, uint32_t end, BtRange* ranges, int64_t ranges_len) {
    if (!ranges || ranges_len <= 0) return true;
    for (int64_t i = 0; i < ranges_len; ++i) {
        if (start < ranges[i].end && end > ranges[i].start) return true;
    }
    return false;
}

void add_highlight(std::vector<BtHighlighedRange>& items, uint32_t start, uint32_t end, uint8_t style, BtRange* ranges, int64_t ranges_len) {
    if (end <= start) return;
    if (!range_overlaps(start, end, ranges, ranges_len)) return;
    items.push_back(BtHighlighedRange{ BtRange{ start, end }, style });
}

__declspec(dllexport) BtResult __cdecl bt_highlight_syntax(char* file_path, char* code_utf8, BtRange* ranges_ptr, int64_t ranges_len, BtHighlightedDiff* out_result) {
    if (!out_result || !code_utf8) { set_error("bt_highlight_syntax: invalid arguments"); return Err; }
    if (!has_supported_highlight_extension(file_path ? file_path : "")) return ErrNotFound;
    std::string code = code_utf8;
    static const std::unordered_set<std::string> keywords = {
        "abstract","as","async","await","base","break","case","catch","checked","class","const","continue","default","delegate","do","else","enum","event","explicit","extern","false","finally","fixed","for","foreach","goto","if","implicit","in","interface","internal","is","lock","namespace","new","null","operator","out","override","params","private","protected","public","readonly","ref","return","sealed","sizeof","stackalloc","static","struct","switch","this","throw","true","try","typeof","unchecked","unsafe","using","virtual","void","volatile","while",
        "auto","bool","char","const","constexpr","double","float","int","long","short","signed","unsigned","template","typename","include","define","ifdef","ifndef","endif","import","package","function","var","let","const","yield","from","export","extends","implements","fn","mut","pub","trait","impl","match","where","type"
    };
    static const std::unordered_set<std::string> types = {
        "string","String","object","Object","Task","List","Dictionary","IEnumerable","IReadOnlyList","var","int","long","short","byte","char","float","double","decimal","bool","boolean","number","void","uint","ulong","usize","isize"
    };
    std::vector<BtHighlighedRange> items;
    uint32_t i = 0;
    uint32_t len = static_cast<uint32_t>(code.size());
    while (i < len) {
        char ch = code[i];
        if (ch == '/' && i + 1 < len && code[i + 1] == '/') {
            uint32_t start = i;
            i += 2;
            while (i < len && code[i] != '\n') ++i;
            add_highlight(items, start, i, 0, ranges_ptr, ranges_len);
            continue;
        }
        if (ch == '/' && i + 1 < len && code[i + 1] == '*') {
            uint32_t start = i;
            i += 2;
            while (i + 1 < len && !(code[i] == '*' && code[i + 1] == '/')) ++i;
            i = (i + 1 < len) ? i + 2 : len;
            add_highlight(items, start, i, 0, ranges_ptr, ranges_len);
            continue;
        }
        if (ch == '#' && (i == 0 || code[i - 1] == '\n')) {
            uint32_t start = i;
            while (i < len && code[i] != '\n') ++i;
            add_highlight(items, start, i, 0, ranges_ptr, ranges_len);
            continue;
        }
        if (ch == '"' || ch == '\'' || ch == '`') {
            char quote = ch;
            uint32_t start = i++;
            bool escape = false;
            while (i < len) {
                char c = code[i++];
                if (escape) {
                    escape = false;
                } else if (c == '\\') {
                    escape = true;
                } else if (c == quote) {
                    break;
                } else if (quote != '`' && c == '\n') {
                    break;
                }
            }
            add_highlight(items, start, i, 1, ranges_ptr, ranges_len);
            continue;
        }
        if ((ch == '<' && i + 1 < len && (std::isalpha(static_cast<unsigned char>(code[i + 1])) || code[i + 1] == '/' || code[i + 1] == '!')) ||
            (ch == '>' && i > 0)) {
            add_highlight(items, i, i + 1, 2, ranges_ptr, ranges_len);
            ++i;
            continue;
        }
        if (ch == '.' && i + 1 < len && is_identifier_start_char(code[i + 1])) {
            uint32_t start = ++i;
            while (i < len && is_identifier_char(code[i])) ++i;
            add_highlight(items, start, i, 6, ranges_ptr, ranges_len);
            continue;
        }
        if (std::isdigit(static_cast<unsigned char>(ch))) {
            uint32_t start = i++;
            while (i < len && (std::isalnum(static_cast<unsigned char>(code[i])) || code[i] == '.' || code[i] == '_' || code[i] == 'x' || code[i] == 'X')) ++i;
            add_highlight(items, start, i, 8, ranges_ptr, ranges_len);
            continue;
        }
        if (ch == '@' && i + 1 < len && is_identifier_start_char(code[i + 1])) {
            uint32_t start = i++;
            while (i < len && is_identifier_char(code[i])) ++i;
            add_highlight(items, start, i, 5, ranges_ptr, ranges_len);
            continue;
        }
        if (is_identifier_start_char(ch)) {
            uint32_t start = i++;
            while (i < len && is_identifier_char(code[i])) ++i;
            std::string word = code.substr(start, i - start);
            if (keywords.find(word) != keywords.end()) {
                add_highlight(items, start, i, 2, ranges_ptr, ranges_len);
            } else if (types.find(word) != types.end() || std::isupper(static_cast<unsigned char>(word[0]))) {
                add_highlight(items, start, i, 3, ranges_ptr, ranges_len);
            }
            continue;
        }
        ++i;
    }
    std::sort(items.begin(), items.end(), [](const BtHighlighedRange& a, const BtHighlighedRange& b) {
        if (a.range_utf16.start != b.range_utf16.start) return a.range_utf16.start < b.range_utf16.start;
        return a.range_utf16.end < b.range_utf16.end;
    });
    assign_vector(out_result->items, out_result->items_len, out_result->items_cap, items);
    return Ok;
}
__declspec(dllexport) void __cdecl bt_release_highlight_syntax(BtHighlightedDiff* r) { if (r) { release_ptr(r->items); r->items_len = r->items_cap = 0; } }

__declspec(dllexport) BtCancellationToken __cdecl bt_new_cancellation_token() { return BtCancellationToken{ new std::atomic_bool(false) }; }
__declspec(dllexport) void __cdecl bt_cancel_cancellation_token(BtCancellationToken* t) { if (t && t->inner) static_cast<std::atomic_bool*>(t->inner)->store(true); }
__declspec(dllexport) void __cdecl bt_release_cancellation_token(BtCancellationToken* t) { if (t && t->inner) { delete static_cast<std::atomic_bool*>(t->inner); t->inner = nullptr; } }
__declspec(dllexport) BtProcessCancellationToken __cdecl bt_new_process_cancellation_token() { return BtProcessCancellationToken{ nullptr }; }
__declspec(dllexport) BtResult __cdecl bt_kill_process_cancellation_token(BtProcessCancellationToken*) { return Ok; }
__declspec(dllexport) void __cdecl bt_release_process_cancellation_token(BtProcessCancellationToken*) {}
__declspec(dllexport) BtCommitGraphCache __cdecl bt_new_commit_graph_cache(char*) { return BtCommitGraphCache{ new RepositoryObjectCache() }; }
__declspec(dllexport) void __cdecl bt_release_commit_graph_cache(BtCommitGraphCache* cache) {
    if (cache && cache->inner) {
        delete static_cast<RepositoryObjectCache*>(cache->inner);
        cache->inner = nullptr;
    }
}

__declspec(dllexport) BtResult __cdecl bt_spawn_with_output(char* path, char* current_dir, char** args_ptr, int64_t args_len, char**, int64_t, uint8_t* stdin_ptr, int64_t stdin_len, BtSpawnWithOutputResult* out_result) {
    if (!out_result) return Err;
    std::string so, se; int status = 1;
    BtResult r = run_process(path, current_dir, args_ptr, args_len, stdin_ptr, stdin_len, so, se, status);
    if (r != Ok) return r;
    out_result->status = status;
    out_result->stdout_data = reinterpret_cast<uint8_t*>(dup_string(so)); out_result->stdout_len = static_cast<int64_t>(so.size()); out_result->stdout_cap = out_result->stdout_len + 1;
    out_result->stderr_data = reinterpret_cast<uint8_t*>(dup_string(se)); out_result->stderr_len = static_cast<int64_t>(se.size()); out_result->stderr_cap = out_result->stderr_len + 1;
    return Ok;
}
__declspec(dllexport) void __cdecl bt_release_spawn_with_output_result(BtSpawnWithOutputResult* r) { if (r) { void* p = r->stdout_data; release_ptr(p); r->stdout_data = nullptr; p = r->stderr_data; release_ptr(p); r->stderr_data = nullptr; } }

__declspec(dllexport) BtResult __cdecl bt_spawn_with_callback(char* path, char* current_dir, char** args_ptr, int64_t args_len, char** env_ptr, int64_t env_len, uint8_t* stdin_ptr, int64_t stdin_len, void* cb_target_ptr, ReadLineCallback read_line_cb, BtProcessCancellationToken*, BtSpawnWithCallbackResult* out_result) {
    std::string so, se; int status = 1;
    BtResult r = run_process(path, current_dir, args_ptr, args_len, stdin_ptr, stdin_len, so, se, status);
    if (r != Ok) return r;
    if (read_line_cb) {
        if (!so.empty()) read_line_cb(cb_target_ptr, 0, reinterpret_cast<uint8_t*>(so.data()), static_cast<int64_t>(so.size()));
        if (!se.empty()) read_line_cb(cb_target_ptr, 1, reinterpret_cast<uint8_t*>(se.data()), static_cast<int64_t>(se.size()));
    }
    if (out_result) out_result->status = status;
    return Ok;
}

__declspec(dllexport) BtResult __cdecl bt_get_head(char* git_dir_path, BtHead* out_result) {
    if (!out_result) return Err;
    std::string head_path = std::string(git_dir_path ? git_dir_path : "") + "/HEAD";
    std::ifstream input(head_path);
    if (!input) {
        set_error("Cannot read HEAD");
        return ErrNotFound;
    }
    std::string head;
    std::getline(input, head);
    if (starts_with(head, "ref: ")) {
        out_result->Reference = dup_string(trim(head.substr(5)));
        out_result->DetachedHead = {};
    } else if (parse_hex_oid(trim(head).c_str(), out_result->DetachedHead)) {
        out_result->Reference = nullptr;
    } else {
        set_error("Cannot parse HEAD");
        return Err;
    }
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_head(BtHead* head) {
    if (head) release_char(head->Reference);
}

struct NativeRefEntry {
    std::string name;
    BtOid oid{};
    bool has_oid = false;
    std::string symref;
    BtOid peeled_oid{};
    bool has_peeled_oid = false;
};

void add_or_update_ref(std::map<std::string, NativeRefEntry>& refs, const NativeRefEntry& entry) {
    auto found = refs.find(entry.name);
    if (found == refs.end()) {
        refs[entry.name] = entry;
        return;
    }
    NativeRefEntry& existing = found->second;
    if (!entry.symref.empty()) {
        existing.symref = entry.symref;
    }
    if (entry.has_oid) {
        existing.oid = entry.oid;
        existing.has_oid = true;
    }
    if (entry.has_peeled_oid) {
        existing.peeled_oid = entry.peeled_oid;
        existing.has_peeled_oid = true;
    }
}

void collect_loose_refs(char* git_dir_path, const std::string& relative_dir, std::map<std::string, NativeRefEntry>& refs) {
    std::string root = std::string(git_dir_path ? git_dir_path : "") + "/" + relative_dir;
    std::string pattern = root + "/*";
    WIN32_FIND_DATAA data{};
    HANDLE find = FindFirstFileA(pattern.c_str(), &data);
    if (find == INVALID_HANDLE_VALUE) return;
    do {
        std::string name = data.cFileName;
        if (name == "." || name == "..") continue;
        std::string child_relative = relative_dir + "/" + name;
        if ((data.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0) {
            collect_loose_refs(git_dir_path, child_relative, refs);
            continue;
        }
        std::string text = trim(read_all_text(std::string(git_dir_path ? git_dir_path : "") + "/" + child_relative));
        NativeRefEntry entry{};
        entry.name = child_relative;
        if (starts_with(text, "ref: ")) {
            entry.symref = trim(text.substr(5));
        } else {
            entry.has_oid = parse_hex_oid(text.c_str(), entry.oid);
        }
        if (entry.has_oid || !entry.symref.empty()) {
            add_or_update_ref(refs, entry);
        }
    } while (FindNextFileA(find, &data));
    FindClose(find);
}

void collect_packed_refs(char* git_dir_path, std::map<std::string, NativeRefEntry>& refs) {
    std::string text = read_all_text(std::string(git_dir_path ? git_dir_path : "") + "/packed-refs");
    if (text.empty()) return;
    std::string last_ref;
    for (const std::string& line : split_lines(text)) {
        if (line.empty() || line[0] == '#') continue;
        if (line[0] == '^') {
            if (!last_ref.empty()) {
                BtOid peeled{};
                if (parse_hex_oid(line.substr(1), peeled)) {
                    NativeRefEntry entry{};
                    entry.name = last_ref;
                    entry.peeled_oid = peeled;
                    entry.has_peeled_oid = true;
                    add_or_update_ref(refs, entry);
                }
            }
            continue;
        }
        size_t space = line.find(' ');
        if (space == std::string::npos) continue;
        NativeRefEntry entry{};
        entry.name = trim(line.substr(space + 1));
        entry.has_oid = parse_hex_oid(line.substr(0, space), entry.oid);
        if (entry.has_oid && starts_with(entry.name, "refs/")) {
            add_or_update_ref(refs, entry);
            last_ref = entry.name;
        }
    }
}

__declspec(dllexport) BtResult __cdecl bt_get_references(char* git_dir_path, bool skip_tags, BtReferences* out_result) {
    if (!out_result) return Err;
    std::map<std::string, NativeRefEntry> refs;
    collect_packed_refs(git_dir_path, refs);
    collect_loose_refs(git_dir_path, "refs", refs);
    std::string head = trim(read_all_text(std::string(git_dir_path ? git_dir_path : "") + "/HEAD"));
    if (!head.empty()) {
        NativeRefEntry head_entry{};
        head_entry.name = "HEAD";
        if (starts_with(head, "ref: ")) {
            head_entry.symref = trim(head.substr(5));
        } else {
            head_entry.has_oid = parse_hex_oid(head.c_str(), head_entry.oid);
        }
        if (head_entry.has_oid || !head_entry.symref.empty()) {
            add_or_update_ref(refs, head_entry);
        }
    }
    std::string names_data;
    std::vector<int64_t> name_offsets;
    std::vector<BtOid> oids;
    std::string symrefs_data;
    std::vector<int64_t> symref_offsets;
    for (auto& item : refs) {
        NativeRefEntry& entry = item.second;
        const std::string& ref_name = entry.name;
        if (ref_name == "FETCH_HEAD" || ref_name == "MERGE_HEAD") continue;
        if (!entry.symref.empty()) {
            symrefs_data += ref_name;
            symref_offsets.push_back(static_cast<int64_t>(symrefs_data.size()));
            symrefs_data += entry.symref;
            symref_offsets.push_back(static_cast<int64_t>(symrefs_data.size()));
            continue;
        }
        if (skip_tags && starts_with(ref_name, "refs/tags/")) continue;
        if (!entry.has_oid) continue;
        BtOid oid = entry.oid;
        if (starts_with(ref_name, "refs/tags/")) {
            if (entry.has_peeled_oid) {
                oid = entry.peeled_oid;
            } else {
                peel_tag_object(git_dir_path, entry.oid, oid);
            }
        }
        names_data += ref_name;
        name_offsets.push_back(static_cast<int64_t>(names_data.size()));
        oids.push_back(oid);
    }
    assign_bytes(out_result->names_data, out_result->names_data_len, out_result->names_data_cap, names_data);
    assign_vector(out_result->names_offsets, out_result->names_offsets_len, out_result->names_offsets_cap, name_offsets);
    assign_vector(out_result->oids, out_result->oids_len, out_result->oids_cap, oids);
    assign_bytes(out_result->symrefs_data, out_result->symrefs_data_len, out_result->symrefs_data_cap, symrefs_data);
    assign_vector(out_result->symrefs_offsets, out_result->symrefs_offsets_len, out_result->symrefs_offsets_cap, symref_offsets);
    std::string hash_data;
    hash_data.reserve(names_data.size() + symrefs_data.size() + oids.size() * 20);
    hash_data += names_data;
    for (const BtOid& oid : oids) {
        uint8_t raw[20];
        oid_to_raw20(oid, raw);
        hash_data.append(reinterpret_cast<const char*>(raw), sizeof(raw));
    }
    hash_data += symrefs_data;
    out_result->hash = fnv1a(hash_data);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_references(BtReferences* r) {
    if (!r) return;
    release_ptr(r->names_data); release_ptr(r->names_offsets); release_ptr(r->oids);
    release_ptr(r->symrefs_data); release_ptr(r->symrefs_offsets);
    r->names_data_len = r->names_data_cap = r->names_offsets_len = r->names_offsets_cap = r->oids_len = r->oids_cap = 0;
    r->symrefs_data_len = r->symrefs_data_cap = r->symrefs_offsets_len = r->symrefs_offsets_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_get_commits(char* git_dir_path, BtOid* tips_ptr, int64_t tips_len, bool date_order, int64_t page_size, int64_t skip_pages, int64_t min_pages, BtOid* required_oids_ptr, int64_t required_oids_len, BtCommitGraphCache* cache_handle, BtCancellationToken* cancellation_token_ptr, BtCommitStorage* out_result) {
    if (!out_result) { set_error("bt_get_commits: out_result is null"); return Err; }
    RepositoryObjectCache& cache = object_cache(git_dir_path, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    int64_t max_count = page_size * std::max<int64_t>(1, min_pages);
    bool use_native_commit_walk = !should_use_git_for_commits(git_dir_path, date_order, skip_pages, cache_handle);
    if (use_native_commit_walk) {
        std::vector<BtOid> tips;
        for (int64_t i = 0; i < tips_len; ++i) tips.push_back(tips_ptr[i]);
        for (int64_t i = 0; i < required_oids_len; ++i) tips.push_back(required_oids_ptr[i]);
        std::vector<BtOid> flat;
        std::vector<uint32_t> indexes;
        bool hit_limit = false;
        int64_t skip_count = page_size > 0 && skip_pages > 0 ? page_size * skip_pages : 0;
        bool native_ok = false;
        if (tips.size() <= 1) {
            native_ok = !tips.empty() && build_commit_storage_native(git_dir_path, tips, date_order, skip_count, max_count, cache_handle, cancellation_token_ptr, flat, indexes, hit_limit);
        } else {
            native_ok = build_commit_storage_priority_native(git_dir_path, tips, skip_count, max_count, cache_handle, cancellation_token_ptr, flat, indexes, hit_limit);
        }
        if (native_ok) {
            assign_vector(out_result->oids, out_result->oids_len, out_result->oids_cap, flat);
            assign_vector(out_result->indexes, out_result->indexes_len, out_result->indexes_cap, indexes);
            out_result->has_more = static_cast<uint8_t>(hit_limit);
            save_persistent_commit_cache(git_dir_path, cache);
            return Ok;
        }
    }
    set_error("bt_get_commits: failed to build native commit storage for " + std::string(git_dir_path ? git_dir_path : ""));
    return Err;
}

__declspec(dllexport) BtResult __cdecl bt_get_commit_subgraph(char* git_dir_path, BtOid* oid, BtCommitGraphCache* cache, BtCommitStorage* out_result) {
    return bt_get_commits(git_dir_path, oid, 1, false, 10000, 0, 1, nullptr, 0, cache, nullptr, out_result);
}

__declspec(dllexport) BtResult __cdecl bt_get_commit_subgraph_2(char* git_dir_path, BtOid* src, BtOid* dst, BtCommitGraphCache* cache, BtCommitStorage* out_result) {
    BtOid tips[2] = { *dst, *src };
    return bt_get_commits(git_dir_path, tips, 2, false, 10000, 0, 1, nullptr, 0, cache, nullptr, out_result);
}

__declspec(dllexport) void __cdecl bt_release_commit_storage(BtCommitStorage* r) {
    if (!r) return;
    release_ptr(r->oids); release_ptr(r->indexes);
    r->oids_len = r->oids_cap = r->indexes_len = r->indexes_cap = 0; r->has_more = 0;
}

__declspec(dllexport) BtResult __cdecl bt_get_revision_headers(char*, char* git_dir_path, BtOid* oids_ptr, int64_t oids_len, BtRevisionHeaders* out_result) {
    if (!out_result) { set_error("bt_get_revision_headers: out_result is null"); return Err; }
    RepositoryObjectCache& cache = object_cache(git_dir_path);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    std::vector<BtRevisionHeader> revisions;
    std::vector<BtIdentity> identities;
    std::map<std::string, int64_t> identity_index;
    for (int64_t i = 0; i < oids_len; ++i) {
        CommitInfo info;
        if (!parse_commit_object(git_dir_path, oids_ptr[i], info)) {
            set_error("bt_get_revision_headers: cannot read commit " + oid_to_hex(oids_ptr[i]));
            return Err;
        }
        std::string key = info.author_name + "\n" + info.author_email;
        int64_t idx;
        auto found = identity_index.find(key);
        if (found == identity_index.end()) {
            idx = static_cast<int64_t>(identities.size());
            identity_index[key] = idx;
            identities.push_back(BtIdentity{ dup_string(info.author_name), dup_string(info.author_email) });
        } else {
            idx = found->second;
        }
        revisions.push_back(BtRevisionHeader{ idx, info.author_time, dup_string(info.subject), static_cast<uint8_t>(!info.body.empty()) });
    }
    assign_vector(out_result->revisions, out_result->revisions_len, out_result->revisions_cap, revisions);
    assign_vector(out_result->identities, out_result->identities_len, out_result->identities_cap, identities);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_revision_headers(BtRevisionHeaders* r) {
    if (!r) return;
    for (int64_t i = 0; i < r->revisions_len; ++i) release_char(reinterpret_cast<BtRevisionHeader*>(r->revisions)[i].subject);
    for (int64_t i = 0; i < r->identities_len; ++i) { release_char(reinterpret_cast<BtIdentity*>(r->identities)[i].name); release_char(reinterpret_cast<BtIdentity*>(r->identities)[i].email); }
    release_ptr(r->revisions); release_ptr(r->identities);
    r->revisions_len = r->revisions_cap = r->identities_len = r->identities_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_get_git_config(char* path, BtGitConfig* out_result) {
    if (!out_result) return Err;
    std::ifstream input(path ? path : "");
    if (!input) {
        out_result->sections = nullptr;
        out_result->sections_len = out_result->sections_cap = 0;
        return Ok;
    }
    struct SectionBuild { std::string name; std::string subsection; std::vector<BtGitConfigVariable> variables; };
    std::vector<SectionBuild> builds;
    SectionBuild* current = nullptr;
    std::string line;
    while (std::getline(input, line)) {
        line = trim(line);
        if (line.empty() || line[0] == '#' || line[0] == ';') continue;
        if (line.front() == '[' && line.back() == ']') {
            std::string header = trim(line.substr(1, line.size() - 2));
            SectionBuild section;
            size_t quote = header.find('"');
            if (quote != std::string::npos) {
                section.name = trim(header.substr(0, quote));
                size_t quote2 = header.rfind('"');
                section.subsection = quote2 > quote ? header.substr(quote + 1, quote2 - quote - 1) : "";
            } else {
                section.name = header;
                section.subsection = "";
            }
            builds.push_back(std::move(section));
            current = &builds.back();
            continue;
        }
        if (!current) continue;
        size_t eq = line.find('=');
        std::string name = trim(eq == std::string::npos ? line : line.substr(0, eq));
        std::string value = trim(eq == std::string::npos ? "true" : line.substr(eq + 1));
        if (value.size() >= 2 && value.front() == '"' && value.back() == '"') value = value.substr(1, value.size() - 2);
        current->variables.push_back(BtGitConfigVariable{ dup_string(name), dup_string(value) });
    }
    std::vector<BtGitConfigSection> sections;
    for (SectionBuild& build : builds) {
        BtGitConfigSection section{};
        section.name = dup_string(build.name);
        section.sub_section = dup_string(build.subsection);
        section.variables = dup_array(build.variables);
        section.variables_len = section.variables_cap = static_cast<int64_t>(build.variables.size());
        sections.push_back(section);
    }
    assign_vector(out_result->sections, out_result->sections_len, out_result->sections_cap, sections);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_git_config(BtGitConfig* r) {
    if (!r) return;
    BtGitConfigSection* sections = reinterpret_cast<BtGitConfigSection*>(r->sections);
    for (int64_t i = 0; i < r->sections_len; ++i) {
        release_char(sections[i].name);
        release_char(sections[i].sub_section);
        BtGitConfigVariable* variables = reinterpret_cast<BtGitConfigVariable*>(sections[i].variables);
        for (int64_t j = 0; j < sections[i].variables_len; ++j) {
            release_char(variables[j].name);
            release_char(variables[j].value);
        }
        release_ptr(sections[i].variables);
    }
    release_ptr(r->sections);
    r->sections_len = r->sections_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_get_behind_ahead_counts(char* git_dir_path, BtOidPair* oid_pairs_ptr, int64_t oid_pairs_len, BtCommitGraphCache* cache_handle, BtBehindAheadCounts* out_result) {
    if (!out_result) { set_error("bt_get_behind_ahead_counts: out_result is null"); return Err; }
    RepositoryObjectCache& cache = object_cache(git_dir_path, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    preload_pack_commit_edges(git_dir_path, cache_handle);
    std::vector<BtBehindAheadCount> items;
    for (int64_t i = 0; i < oid_pairs_len; ++i) {
        std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>> left_reachable = get_reachable_commits(git_dir_path, oid_pairs_ptr[i].left, cache_handle);
        std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>> right_reachable = get_reachable_commits(git_dir_path, oid_pairs_ptr[i].right, cache_handle);
        if (!left_reachable || !right_reachable) {
            set_error("bt_get_behind_ahead_counts: cannot read graph for " + oid_to_hex(oid_pairs_ptr[i].left) + "..." + oid_to_hex(oid_pairs_ptr[i].right));
            return Err;
        }
        uint32_t left = 0;
        uint32_t right = 0;
        for (const BtOid& oid : *left_reachable) {
            if (right_reachable->find(oid) == right_reachable->end()) {
                ++left;
            }
        }
        for (const BtOid& oid : *right_reachable) {
            if (left_reachable->find(oid) == left_reachable->end()) {
                ++right;
            }
        }
        items.push_back(BtBehindAheadCount{ left, right });
    }
    assign_vector(out_result->items, out_result->items_len, out_result->items_cap, items);
    save_persistent_commit_cache(git_dir_path, cache);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_behind_ahead_counts(BtBehindAheadCounts* r) {
    if (r) { release_ptr(r->items); r->items_len = r->items_cap = 0; }
}

__declspec(dllexport) BtResult __cdecl bt_get_committer_times(char* git_dir_path, BtOid* oids_ptr, int64_t oids_len, BtCommitGraphCache* cache_handle, BtCommitterTimes* out_result) {
    if (!out_result) { set_error("bt_get_committer_times: out_result is null"); return Err; }
    RepositoryObjectCache& cache = object_cache(git_dir_path, cache_handle);
    std::lock_guard<std::recursive_mutex> lock(cache.mutex);
    std::vector<int64_t> times;
    for (int64_t i = 0; i < oids_len; ++i) {
        auto cached = cache.committer_times.find(oids_ptr[i]);
        if (cached != cache.committer_times.end()) {
            times.push_back(cached->second);
            continue;
        }
        const CachedCommitEdges* edges = get_commit_edges_cached(git_dir_path, oids_ptr[i], cache_handle);
        if (edges && edges->committer_time != 0) {
            cache.committer_times[oids_ptr[i]] = edges->committer_time;
            times.push_back(edges->committer_time);
            continue;
        }
        int64_t committer_time = 0;
        if (parse_commit_committer_time(git_dir_path, oids_ptr[i], committer_time, cache_handle)) {
            cache.committer_times[oids_ptr[i]] = committer_time;
            times.push_back(committer_time);
            continue;
        }
        cache.committer_times[oids_ptr[i]] = 0;
        times.push_back(0);
    }
    assign_vector(out_result->times, out_result->times_len, out_result->times_cap, times);
    save_persistent_commit_cache(git_dir_path, cache);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_committer_times(BtCommitterTimes* r) {
    if (r) { release_ptr(r->times); r->times_len = r->times_cap = 0; }
}

__declspec(dllexport) BtResult __cdecl bt_get_tag_details(char* git_dir_path, BtOid tag_oid, BtTagDetails* out_result) {
    if (!out_result) { set_error("bt_get_tag_details: out_result is null"); return Err; }
    GitObject object;
    if (read_git_object(git_dir_path, tag_oid, object) && object.type == "tag") {
        std::string content(reinterpret_cast<char*>(object.data.data()), object.data.size());
        std::istringstream input(content);
        std::string line;
        std::string message;
        bool in_message = false;
        while (std::getline(input, line)) {
            if (!line.empty() && line.back() == '\r') line.pop_back();
            if (in_message) {
                message += line;
                message += "\n";
                continue;
            }
            if (line.empty()) {
                in_message = true;
                continue;
            }
            if (starts_with(line, "object ")) {
                parse_hex_oid(line.substr(7), out_result->tag_object_oid);
            } else if (starts_with(line, "tag ")) {
                out_result->name = dup_string(line.substr(4));
            } else if (starts_with(line, "tagger ")) {
                std::string tagger = line.substr(7);
                size_t lt = tagger.find('<');
                size_t gt = tagger.find('>');
                if (lt != std::string::npos && gt != std::string::npos && gt > lt) {
                    out_result->tagger_name = dup_string(trim(tagger.substr(0, lt)));
                    out_result->tagger_email = dup_string(tagger.substr(lt + 1, gt - lt - 1));
                    out_result->tagger_time = _strtoi64(tagger.substr(gt + 1).c_str(), nullptr, 10);
                }
            }
        }
        while (!message.empty() && (message.back() == '\n' || message.back() == '\r')) message.pop_back();
        out_result->message = dup_string(message);
        if (!out_result->name) out_result->name = dup_string("");
        if (!out_result->tagger_name) out_result->tagger_name = dup_string("");
        if (!out_result->tagger_email) out_result->tagger_email = dup_string("");
        return Ok;
    }
    set_error("bt_get_tag_details: cannot read tag object " + oid_to_hex(tag_oid));
    return Err;
}

__declspec(dllexport) void __cdecl bt_release_tag_details(BtTagDetails* r) {
    if (!r) return;
    release_char(r->tagger_name); release_char(r->tagger_email); release_char(r->name); release_char(r->message);
}

__declspec(dllexport) BtResult __cdecl bt_find_fartherest_tip(char* git_dir_path, BtOid*, BtOid* tips_ptr, int64_t tips_len, BtOid* base_oid, BtCommitGraphCache* cache_handle, BtOid* out_result) {
    if (!out_result || !base_oid) { set_error("bt_find_fartherest_tip: invalid arguments"); return Err; }
    std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>> base_reachable = get_reachable_commits(git_dir_path, *base_oid, cache_handle);
    if (!base_reachable) { set_error("bt_find_fartherest_tip: cannot read base " + oid_to_hex(*base_oid)); return Err; }
    int best_count = -1;
    BtOid best = tips_len > 0 ? tips_ptr[0] : *base_oid;
    for (int64_t i = 0; i < tips_len; ++i) {
        std::shared_ptr<std::unordered_set<BtOid, BtOidHash, BtOidEqual>> tip_reachable = get_reachable_commits(git_dir_path, tips_ptr[i], cache_handle);
        if (!tip_reachable) continue;
        int count = 0;
        for (const BtOid& oid : *tip_reachable) {
            if (base_reachable->find(oid) == base_reachable->end()) {
                ++count;
            }
        }
        if (count > best_count) {
            best_count = count;
            best = tips_ptr[i];
        }
    }
    *out_result = best;
    return Ok;
}

__declspec(dllexport) BtResult __cdecl bt_get_tree(char* git_dir_path, BtOid* oid, BtTree* out_result) {
    if (!out_result || !oid) { set_error("bt_get_tree: invalid arguments"); return Err; }
    GitObject object;
    if (read_git_object(git_dir_path, *oid, object) && object.type == "tree") {
        std::vector<BtTreeItem> entries;
        size_t pos = 0;
        while (pos < object.data.size()) {
            size_t mode_end = pos;
            while (mode_end < object.data.size() && object.data[mode_end] != ' ') mode_end++;
            if (mode_end >= object.data.size()) break;
            std::string mode(reinterpret_cast<char*>(object.data.data() + pos), mode_end - pos);
            size_t name_start = mode_end + 1;
            size_t name_end = name_start;
            while (name_end < object.data.size() && object.data[name_end] != 0) name_end++;
            if (name_end + 20 >= object.data.size() + 1) break;
            std::string name(reinterpret_cast<char*>(object.data.data() + name_start), name_end - name_start);
            BtOid child = oid_from_raw20(object.data.data() + name_end + 1);
            uint16_t kind = static_cast<uint16_t>(std::strtoul(mode.c_str(), nullptr, 8));
            entries.push_back(BtTreeItem{ kind, dup_string(name), child });
            pos = name_end + 1 + 20;
        }
        assign_vector(out_result->entries, out_result->entries_len, out_result->entries_cap, entries);
        return Ok;
    }
    set_error("bt_get_tree: cannot read tree object " + oid_to_hex(*oid));
    return Err;
}

__declspec(dllexport) void __cdecl bt_release_tree(BtTree* r) {
    if (!r) return;
    BtTreeItem* entries = reinterpret_cast<BtTreeItem*>(r->entries);
    for (int64_t i = 0; i < r->entries_len; ++i) {
        release_char(entries[i].filename);
    }
    release_ptr(r->entries);
    r->entries_len = r->entries_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_get_repository_stashes(char*, char* git_dir_path, BtRepositoryStashes* out_result) {
    if (!out_result) return Err;
    std::string reflog = read_all_text(std::string(git_dir_path ? git_dir_path : "") + "/logs/refs/stash");
    if (reflog.empty()) {
        out_result->stashes = nullptr; out_result->stashes_len = out_result->stashes_cap = 0;
        out_result->identities = nullptr; out_result->identities_len = out_result->identities_cap = 0;
        return Ok;
    }
    std::vector<BtStash> stashes;
    std::vector<BtIdentity> identities;
    std::map<std::string, int64_t> identity_index;
    std::vector<std::string> lines = split_lines(reflog);
    for (int64_t line_index = static_cast<int64_t>(lines.size()) - 1, reflog_id = 0; line_index >= 0; --line_index, ++reflog_id) {
        const std::string& line = lines[static_cast<size_t>(line_index)];
        if (line.size() < 82) continue;
        size_t first_space = line.find(' ');
        if (first_space == std::string::npos || first_space + 41 > line.size()) continue;
        std::string new_oid_text = line.substr(first_space + 1, 40);
        BtStash stash{};
        if (!parse_hex_oid(new_oid_text.c_str(), stash.oid)) continue;
        CommitInfo info;
        if (!parse_commit_object(git_dir_path, stash.oid, info)) continue;
        if (!info.parents.empty()) {
            stash.first_parent = info.parents[0];
        }
        std::string key = info.author_name + "\n" + info.author_email;
        auto found = identity_index.find(key);
        if (found == identity_index.end()) {
            stash.author_index = static_cast<int64_t>(identities.size());
            identity_index[key] = stash.author_index;
            identities.push_back(BtIdentity{ dup_string(info.author_name), dup_string(info.author_email) });
        } else {
            stash.author_index = found->second;
        }
        stash.author_time = info.author_time;
        stash.reflog_id = static_cast<int32_t>(reflog_id);
        std::string subject = info.subject;
        if (subject.empty()) {
            size_t tab = line.find('\t');
            if (tab != std::string::npos) {
                subject = line.substr(tab + 1);
            }
        }
        stash.subject = dup_string(subject);
        stashes.push_back(stash);
    }
    assign_vector(out_result->stashes, out_result->stashes_len, out_result->stashes_cap, stashes);
    assign_vector(out_result->identities, out_result->identities_len, out_result->identities_cap, identities);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_repository_stashes(BtRepositoryStashes* r) {
    if (!r) return;
    BtStash* stashes = reinterpret_cast<BtStash*>(r->stashes);
    for (int64_t i = 0; i < r->stashes_len; ++i) release_char(stashes[i].subject);
    BtIdentity* identities = reinterpret_cast<BtIdentity*>(r->identities);
    for (int64_t i = 0; i < r->identities_len; ++i) { release_char(identities[i].name); release_char(identities[i].email); }
    release_ptr(r->stashes); release_ptr(r->identities);
    r->stashes_len = r->stashes_cap = r->identities_len = r->identities_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_search_commits(char* git_dir_path, BtOid* oids_ptr, int64_t oids_len, char* query, BtOid*, int64_t, BtCancellationToken* cancellation_token_ptr, BtSearchCommitsResult* out_result) {
    if (!out_result) return Err;
    std::string needle = query ? query : "";
    std::transform(needle.begin(), needle.end(), needle.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
    std::vector<BtOid> matches;
    for (int64_t i = 0; i < oids_len; ++i) {
        if (cancellation_token_ptr && cancellation_token_ptr->inner && static_cast<std::atomic_bool*>(cancellation_token_ptr->inner)->load()) return ErrCanceled;
        CommitInfo info;
        if (!parse_commit_object(git_dir_path, oids_ptr[i], info)) continue;
        std::string haystack = info.subject + "\n" + info.body;
        std::transform(haystack.begin(), haystack.end(), haystack.begin(), [](unsigned char c) { return static_cast<char>(std::tolower(c)); });
        if (needle.empty() || haystack.find(needle) != std::string::npos) {
            matches.push_back(oids_ptr[i]);
        }
    }
    assign_vector(out_result->matches, out_result->matches_len, out_result->matches_cap, matches);
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_search_commits(BtSearchCommitsResult* r) {
    if (r) { release_ptr(r->matches); r->matches_len = r->matches_cap = 0; }
}

#define STUB_RESULT(name, args) __declspec(dllexport) BtResult __cdecl name args { set_error(#name " is not implemented in ForkPlus.Biturbo yet"); return ErrNotFound; }
#define STUB_RELEASE(name, type) __declspec(dllexport) void __cdecl name(type*) {}
__declspec(dllexport) BtResult __cdecl bt_get_repository_manager(char* path, BtRepositoryManager* out_result) {
    if (!out_result) return Err;
    std::ifstream input(path ? path : "");
    std::vector<std::string> source_dirs;
    std::vector<std::string> ignore;
    uint8_t scan_depth = 2;
    std::vector<BtRepositoryManagerRepository> repositories;
    BtRepositoryManagerRepository current{};
    bool in_repo = false;
    auto flush_repo = [&]() {
        if (in_repo && current.path) repositories.push_back(current);
        current = {};
        in_repo = false;
    };
    if (input) {
        std::string line;
        while (std::getline(input, line)) {
            line = trim(line);
            if (line.empty() || line[0] == '#') continue;
            if (line == "[[repositories]]" || line == "[[repository]]") {
                flush_repo();
                in_repo = true;
                continue;
            }
            size_t eq = line.find('=');
            if (eq == std::string::npos) continue;
            std::string key = trim(line.substr(0, eq));
            std::string value = trim(line.substr(eq + 1));
            if (!in_repo) {
                if (key == "source_dirs" || key == "sourceDirs") source_dirs = parse_string_array(value);
                else if (key == "ignore") ignore = parse_string_array(value);
                else if (key == "scan_depth" || key == "scanDepth") scan_depth = static_cast<uint8_t>(std::strtoul(value.c_str(), nullptr, 10));
            } else {
                if (key == "path") current.path = dup_string(unquote(value));
                else if (key == "alias") {
                    std::string alias = unquote(value);
                    current.alias = alias.empty() ? nullptr : dup_string(alias);
                } else if (key == "opened") current.opened = static_cast<uint32_t>(std::strtoul(value.c_str(), nullptr, 10));
                else if (key == "color") current.color = static_cast<uint8_t>(std::strtoul(value.c_str(), nullptr, 10));
            }
        }
        flush_repo();
    }
    std::vector<char*> source_ptrs;
    for (const auto& value : source_dirs) source_ptrs.push_back(dup_string(value));
    std::vector<char*> ignore_ptrs;
    for (const auto& value : ignore) ignore_ptrs.push_back(dup_string(value));
    out_result->source_dirs = dup_array(source_ptrs);
    out_result->source_dirs_len = out_result->source_dirs_cap = static_cast<int64_t>(source_ptrs.size());
    out_result->scan_depth = scan_depth;
    out_result->ignore = dup_array(ignore_ptrs);
    out_result->ignore_len = out_result->ignore_cap = static_cast<int64_t>(ignore_ptrs.size());
    out_result->repositories = dup_array(repositories);
    out_result->repositories_len = out_result->repositories_cap = static_cast<int64_t>(repositories.size());
    return Ok;
}

__declspec(dllexport) void __cdecl bt_release_repository_manager(BtRepositoryManager* r) {
    if (!r) return;
    char** source_dirs = reinterpret_cast<char**>(r->source_dirs);
    for (int64_t i = 0; i < r->source_dirs_len; ++i) release_char(source_dirs[i]);
    char** ignore = reinterpret_cast<char**>(r->ignore);
    for (int64_t i = 0; i < r->ignore_len; ++i) release_char(ignore[i]);
    BtRepositoryManagerRepository* repos = reinterpret_cast<BtRepositoryManagerRepository*>(r->repositories);
    for (int64_t i = 0; i < r->repositories_len; ++i) {
        release_char(repos[i].path);
        release_char(repos[i].alias);
    }
    release_ptr(r->source_dirs); release_ptr(r->ignore); release_ptr(r->repositories);
    r->source_dirs_len = r->source_dirs_cap = r->ignore_len = r->ignore_cap = r->repositories_len = r->repositories_cap = 0;
}

__declspec(dllexport) BtResult __cdecl bt_save_repository_manager(char* path, char** source_dirs_ptr, int64_t source_dirs_len, uint8_t scan_depth, char** ignore_ptr, int64_t ignore_len, char** paths_ptr, int64_t paths_len, char** aliases_ptr, int64_t aliases_len, uint32_t* opened_ptr, int64_t opened_len, uint8_t* colors_ptr, int64_t colors_len) {
    std::ofstream output(path ? path : "", std::ios::trunc);
    if (!output) {
        set_error("Cannot write repository manager file");
        return Err;
    }
    output << "source_dirs = " << format_string_array(source_dirs_ptr, source_dirs_len) << "\n";
    output << "scan_depth = " << static_cast<int>(scan_depth) << "\n";
    output << "ignore = " << format_string_array(ignore_ptr, ignore_len) << "\n\n";
    for (int64_t i = 0; i < paths_len; ++i) {
        output << "[[repositories]]\n";
        output << "path = " << quote_toml(paths_ptr[i] ? paths_ptr[i] : "") << "\n";
        std::string alias = (i < aliases_len && aliases_ptr[i]) ? aliases_ptr[i] : "";
        output << "alias = " << quote_toml(alias) << "\n";
        output << "opened = " << ((i < opened_len) ? opened_ptr[i] : 0) << "\n";
        output << "color = " << static_cast<int>((i < colors_len) ? colors_ptr[i] : 0) << "\n\n";
    }
    return Ok;
}

}
