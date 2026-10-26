"""Check repository-local Markdown destinations and basic document structure."""
import html
from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]


def check():
    candidates = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z"], cwd=ROOT
    ).decode().split("\0")
    documents = sorted({ROOT / name for name in candidates if name.endswith(".md")})
    errors = []
    checked = 0
    external = set()
    for path in documents:
        content = path.read_text(encoding="utf-8")
        if not content.endswith("\n"):
            errors.append(f"{path.relative_to(ROOT)}: missing final newline")
        fences = re.findall(r"^\s*```", content, re.M)
        if len(fences) % 2:
            errors.append(f"{path.relative_to(ROOT)}: unbalanced code fences")
        # Exclude examples/code; actual links may use inline HTML in GitHub READMEs.
        body = re.sub(r"```.*?```", "", content, flags=re.S)
        links = re.findall(r"!?\[[^\]]*\]\(([^\s)]+)(?:\s+[^)]*)?\)", body)
        links += re.findall(r'(?:href|src)="([^"]+)"', body)
        links += re.findall(r"^\[[^\]]+\]:\s*(\S+)", body, re.M)
        for destination in links:
            destination = html.unescape(destination.strip("<>"))
            parsed = urlsplit(destination)
            if parsed.scheme or parsed.netloc:
                external.add(destination)
                continue
            if not parsed.path:
                continue
            target = (path.parent / unquote(parsed.path)).resolve()
            checked += 1
            if not target.is_file() or not target.is_relative_to(ROOT):
                errors.append(f"{path.relative_to(ROOT)}: missing/invalid destination {destination}")
        for image in re.findall(r"<img\b[^>]*>", body):
            if not re.search(r'\balt="[^"]+"', image):
                errors.append(f"{path.relative_to(ROOT)}: image missing meaningful alt text")
    for error in errors:
        print(error)
    print(f"Checked {len(documents)} Markdown files, {checked} local links/images; {len(errors)} errors.")
    print(f"External destinations: {len(external)} (network checks are separate).")
    return bool(errors)


if __name__ == "__main__":
    sys.exit(check())
