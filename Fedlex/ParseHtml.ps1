Get-ChildItem -Path . -Recurse -File -Include *.html | Where-Object {
  $_.DirectoryName -notmatch '\\markdown($|\\)'
} | ForEach-Object {
  docling -v --from html --to md --output "./Markdown" $_.FullName
}