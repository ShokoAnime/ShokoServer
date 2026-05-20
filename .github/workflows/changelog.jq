.[] |
  "\n`\(.type)`" +
  if .scope then " (`\(.scope)`)" else "" end +
  ": **\(.subject)**" +
  if .prNumber then " ([#\(.prNumber)](https://github.com/ShokoAnime/ShokoServer/pull/\(.prNumber)))" else "" end +
  if .body != null and .body != "" then
    if .isSkipCI then
      ": (_Skip CI_)\n\n\(.body)"
    else
      ":\n\n\(.body)"
    end
  else
    if .isSkipCI then
      ". (_Skip CI_)"
    else
      "."
    end
  end
