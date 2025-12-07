import os
import re
from pathlib import Path


C_LIKE_EXTS = {".js", ".jsx", ".ts", ".tsx", ".cs", ".c", ".cpp", ".h", ".mjs"}
XML_LIKE_EXTS = {".xaml", ".xml", ".csproj", ".resx", ".config", ".axml"}
HASH_STYLE_EXTS = {".ps1", ".sh", ".bash"}
SKIP_DIRS = {".git", "bin", "obj", "node_modules", ".vs", ".vscode", "__pycache__"}


def strip_c_style(text: str) -> str:
    result = []
    i = 0
    in_string = None
    in_block = False
    in_line = False
    length = len(text)
    while i < length:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < length else ""
        if in_line:
            if ch == "\n":
                result.append(ch)
                in_line = False
            i += 1
            continue
        if in_block:
            if ch == "*" and nxt == "/":
                in_block = False
                i += 2
            else:
                i += 1
            continue
        if in_string:
            result.append(ch)
            if ch == "\\" and i + 1 < length:
                result.append(text[i + 1])
                i += 2
                continue
            if ch == in_string:
                in_string = None
            i += 1
            continue
        if ch in ('"', "'", "`"):
            in_string = ch
            result.append(ch)
            i += 1
            continue
        if ch == "/" and nxt == "/":
            in_line = True
            i += 2
            continue
        if ch == "/" and nxt == "*":
            in_block = True
            i += 2
            continue
        result.append(ch)
        i += 1
    return "".join(result)


def strip_xml_style(text: str) -> str:
    return re.sub(r"<!--.*?-->", "", text, flags=re.S)


def strip_hash_style(text: str) -> str:
    result = []
    i = 0
    in_string = None
    in_block = False
    length = len(text)
    while i < length:
        ch = text[i]
        nxt = text[i + 1] if i + 1 < length else ""
        if in_block:
            if ch == "#" and nxt == ">":
                in_block = False
                i += 2
            else:
                i += 1
            continue
        if in_string:
            result.append(ch)
            if ch == "\\" and i + 1 < length:
                result.append(text[i + 1])
                i += 2
                continue
            if ch == in_string:
                in_string = None
            i += 1
            continue
        if ch in ('"', "'"):
            in_string = ch
            result.append(ch)
            i += 1
            continue
        if ch == "<" and nxt == "#":
            in_block = True
            i += 2
            continue
        if ch == "#":
            while i < length and text[i] != "\n":
                i += 1
            continue
        result.append(ch)
        i += 1
    return "".join(result)


def should_skip_dir(path: Path) -> bool:
    return any(part in SKIP_DIRS for part in path.parts)


def process_file(path: Path) -> None:
    ext = path.suffix.lower()
    if ext not in C_LIKE_EXTS | XML_LIKE_EXTS | HASH_STYLE_EXTS:
        return
    original = path.read_text(encoding="utf-8")
    if ext in C_LIKE_EXTS:
        updated = strip_c_style(original)
    elif ext in XML_LIKE_EXTS:
        updated = strip_xml_style(original)
    else:
        updated = strip_hash_style(original)
    if updated != original:
        path.write_text(updated, encoding="utf-8")


def main() -> None:
    root = Path(__file__).parent
    for dirpath, dirnames, filenames in os.walk(root):
        current = Path(dirpath)
        dirnames[:] = [d for d in dirnames if not should_skip_dir(current / d)]
        for filename in filenames:
            process_file(current / filename)


if __name__ == "__main__":
    main()
