#!/usr/bin/env python3
"""
A303-314 Phase 2 — MVPArena.unity SceneRoots-aware splice script.

Algorithm (per plan §5 Step 3):
1. Extract 4 GO bundle (CardSystem 472722521, CardSelectionUI 583588488,
   HPBarManager 670270459, HPBarCanvas 965082887) + all sub-components
   from develop tip (origin/develop).
2. Splice PR-2 source MVPArena.unity at SceneRoots marker line:
   pre-marker + bundle + marker + SceneRoots block with patched m_Roots[].
3. Append 3 true-root Transform fileIDs to m_Roots[]: 965082891, 472722524, 583588490
   (HPBarManager Transform 670270460 EXCLUDED — m_Father=965082891 child).
4. Verify U1~U8 unit gates per plan §4.1.

Inputs (constants below):
- DEV_TIP_REV: develop tip SHA (origin/develop)
- TARGET_FILE: path to MVPArena.unity (already at PR-2 source state)
- 4 GO top-level + component fileID definitions

Outputs:
- Patched TARGET_FILE
- .tmp/phase2-4go-bundle.txt (extracted bundle)
- .tmp/phase2-step3-evidence.txt (U-gate results + sha256s)
"""

from __future__ import annotations

import hashlib
import os
import re
import subprocess
import sys
from pathlib import Path

# -- Constants (plan §1.1-§1.7 verified) --
REPO_ROOT = Path(__file__).resolve().parent.parent
TARGET_FILE = REPO_ROOT / "unity" / "Assets" / "_Project" / "Scenes" / "Greybox" / "MVPArena.unity"
BUNDLE_DUMP = REPO_ROOT / ".tmp" / "phase2-4go-bundle.txt"
EVIDENCE_FILE = REPO_ROOT / ".tmp" / "phase2-step3-evidence.txt"
DEV_TIP_REV = "origin/develop"
SCENE_FILE_REL = "unity/Assets/_Project/Scenes/Greybox/MVPArena.unity"

# 4 GO top-level fileIDs (per plan §1.2)
GO_FILEIDS = [
    ("CardSystem", "472722521"),
    ("CardSelectionUI", "583588488"),
    ("HPBarManager", "670270459"),
    ("HPBarCanvas", "965082887"),
]

# True-root Transform fileIDs (per plan §1.7) — HPBarManager Transform 670270460 EXCLUDED
M_ROOTS_APPEND = ["965082891", "472722524", "583588490"]

SCENEROOTS_MARKER = "--- !u!1660057539 &9223372036854775807"


def sha256_bytes(b: bytes) -> str:
    return hashlib.sha256(b).hexdigest()


def git_show(rev: str, path: str) -> str:
    """Return file content from a git revision (utf-8 text, LF newlines)."""
    out = subprocess.run(
        ["git", "show", f"{rev}:{path}"],
        cwd=REPO_ROOT,
        capture_output=True,
        check=True,
    )
    # Decode as utf-8; preserve LF newlines (Unity scene files are LF)
    return out.stdout.decode("utf-8")


def parse_block(text: str, fileid: str) -> tuple[int, int, str]:
    """
    Locate `--- !u!1 &<fileid>$` block in text. Return (start_offset, end_offset, block_text).
    end_offset is the start of the NEXT `^--- !u!` marker (or len(text) at EOF).
    """
    start_pat = re.compile(rf"^--- !u!1 &{fileid}$", re.MULTILINE)
    sm = start_pat.search(text)
    if not sm:
        raise RuntimeError(f"GameObject fileID &{fileid} not found")
    start = sm.start()
    next_pat = re.compile(r"^--- !u!", re.MULTILINE)
    nm = next_pat.search(text, sm.end())
    end = nm.start() if nm else len(text)
    return start, end, text[start:end]


def parse_component_ids(go_block: str) -> list[str]:
    """
    Parse m_Component fileIDs from a GameObject block.
    Format inside block:
      m_Component:
      - component: {fileID: 472722522}
      - component: {fileID: 472722523}
      - component: {fileID: 472722524}
    """
    ids: list[str] = []
    in_comp = False
    for line in go_block.splitlines():
        stripped = line.lstrip()
        if stripped.startswith("m_Component:"):
            in_comp = True
            continue
        if in_comp:
            m = re.match(r"^\s*-\s*component:\s*\{fileID:\s*(\d+)\}", line)
            if m:
                ids.append(m.group(1))
                continue
            # Next field (not starting with `-`) ends the component list
            if line and not line.startswith(" ") and not line.startswith("\t"):
                break
            # A top-level GO field (no leading `-`) at scene-block indent ends list
            if stripped and not stripped.startswith("-") and ":" in stripped:
                break
    return ids


def find_component_block(text: str, comp_fileid: str) -> tuple[int, int, str]:
    """
    Locate `--- !u!<TYPE> &<fileid>$` for any TYPE. Return (start, end, block_text).
    """
    pat = re.compile(rf"^--- !u!\d+ &{comp_fileid}$", re.MULTILINE)
    sm = pat.search(text)
    if not sm:
        raise RuntimeError(f"component fileID &{comp_fileid} not found in dev tip")
    start = sm.start()
    next_pat = re.compile(r"^--- !u!", re.MULTILINE)
    nm = next_pat.search(text, sm.end())
    end = nm.start() if nm else len(text)
    return start, end, text[start:end]


def build_bundle(dev_tip_text: str) -> tuple[str, dict[str, list[str]]]:
    """
    Build concatenated 4 GO + sub-component bundle from dev tip text.
    Returns (bundle_text, {go_name: [component_fileids]}).
    """
    bundle_parts: list[str] = []
    comp_map: dict[str, list[str]] = {}
    for name, fid in GO_FILEIDS:
        _, _, go_block = parse_block(dev_tip_text, fid)
        comp_ids = parse_component_ids(go_block)
        comp_map[name] = comp_ids
        bundle_parts.append(go_block)
        for cid in comp_ids:
            _, _, cblock = find_component_block(dev_tip_text, cid)
            bundle_parts.append(cblock)
    bundle = "".join(bundle_parts)
    # Each block already ends with newline from the slice; ensure final newline
    if not bundle.endswith("\n"):
        bundle += "\n"
    return bundle, comp_map


def find_sceneroots_split(target_text: str) -> int:
    """Return char offset where `--- !u!1660057539 &9223372036854775807$` begins."""
    pat = re.compile(r"^--- !u!1660057539 &9223372036854775807$", re.MULTILINE)
    m = pat.search(target_text)
    if not m:
        raise RuntimeError("SceneRoots marker not found in target file")
    return m.start()


def patch_m_roots(sceneroots_section: str) -> str:
    """
    Append the 3 true-root Transform fileIDs to the END of m_Roots: array
    within the SceneRoots section.
    The m_Roots block has entries like:
      m_Roots:
      - {fileID: 1234}
      - {fileID: 5678}
      ...
    We find the LAST `^  - {fileID: \d+}$` line in the section and insert after its newline.
    """
    # Find all m_Roots entry positions (in section-relative offsets)
    entry_pat = re.compile(r"^  - \{fileID: \d+\}\s*$", re.MULTILINE)
    matches = list(entry_pat.finditer(sceneroots_section))
    if not matches:
        raise RuntimeError("m_Roots[] entries not found in SceneRoots section")
    last = matches[-1]
    # Insert after the end-of-line of the last entry
    insert_pos = last.end()
    # Ensure newline follows
    if insert_pos < len(sceneroots_section) and sceneroots_section[insert_pos] != "\n":
        # last.end() should point at newline char position; safe append
        addendum = "\n" + "\n".join(f"  - {{fileID: {fid}}}" for fid in M_ROOTS_APPEND)
    else:
        # last.end() points at end of line content (before newline); insert after newline
        addendum = "\n" + "\n".join(f"  - {{fileID: {fid}}}" for fid in M_ROOTS_APPEND)
    # Move past the trailing newline if present so addendum slots in as new lines
    if insert_pos < len(sceneroots_section) and sceneroots_section[insert_pos] == "\n":
        insert_pos += 1
        addendum = "\n".join(f"  - {{fileID: {fid}}}" for fid in M_ROOTS_APPEND) + "\n"
    return sceneroots_section[:insert_pos] + addendum + sceneroots_section[insert_pos:]


def run_u_gates(post_text: str, dev_tip_bundle_sha: str, bundle_text: str) -> dict[str, str]:
    """
    Run U1, U2, U3-skip-here (script gate), U4, U6, U6b, U7, U8 unit gates.
    Returns dict gate_id -> "PASS: ..." or "FAIL: ..." string.
    """
    results: dict[str, str] = {}

    # U1: 4 m_Name across 4 GO
    u1_names = re.findall(
        r"^\s+m_Name: (CardSystem|CardSelectionUI|HPBarManager|HPBarCanvas)\s*$",
        post_text,
        re.MULTILINE,
    )
    results["U1"] = (
        f"PASS: m_Name matches={len(u1_names)} (expected 4)"
        if len(u1_names) == 4
        else f"FAIL: m_Name matches={len(u1_names)} (expected 4)"
    )

    # U2: 4 top-level GO definitions
    u2 = re.findall(
        r"^--- !u!1 &(472722521|583588488|670270459|965082887)$",
        post_text,
        re.MULTILINE,
    )
    results["U2"] = (
        f"PASS: top-level GO definitions={len(u2)} (expected 4)"
        if len(u2) == 4
        else f"FAIL: top-level GO definitions={len(u2)} (expected 4)"
    )

    # U3: 4 Transform/RectTransform definitions (Phase 1 PNG GUID resolution out-of-scope here)
    u3 = re.findall(
        r"^--- !u!(?:4|224) &(472722524|583588490|670270460|965082891)$",
        post_text,
        re.MULTILINE,
    )
    results["U3"] = (
        f"PASS: 4 Transform/RectTransform definitions={len(u3)} (expected 4)"
        if len(u3) == 4
        else f"FAIL: 4 Transform/RectTransform definitions={len(u3)} (expected 4)"
    )

    # U4: BackgroundParallax MonoBehaviour block integrity (iter3 boundary-anchored)
    u4_m = re.search(
        r"^--- !u!114 &2063737743\nMonoBehaviour:.*?guid: 8272885def2c5fd48a322c0b7f923b60.*?targetCamera: \{fileID: (\d+)\}.*?(?=^--- )",
        post_text,
        re.DOTALL | re.MULTILINE,
    )
    if not u4_m:
        results["U4"] = "FAIL: BackgroundParallax MB block pattern not found"
    elif u4_m.group(1) == "0":
        results["U4"] = f"FAIL: targetCamera null (fileID=0)"
    else:
        span = post_text[u4_m.start() : u4_m.end()]
        # Skip the leading `--- !u!` and ensure no other `^--- !u!` boundary within span
        if re.search(r"^--- !u!", span[1:], re.MULTILINE):
            results["U4"] = "FAIL: block boundary violated (intervening !u! marker)"
        else:
            results["U4"] = f"PASS: BackgroundParallax targetCamera fileID={u4_m.group(1)}"

    # U6: m_Roots[] contains exactly 3 of (965082891, 472722524, 583588490) after SceneRoots:
    # awk-equivalent: from `^SceneRoots:` to EOF, count lines `^  - {fileID: <id>}$`
    sr_match = re.search(r"^SceneRoots:", post_text, re.MULTILINE)
    u6_count = 0
    u6b_count = 0
    if sr_match:
        sr_tail = post_text[sr_match.start():]
        u6_count = len(
            re.findall(
                r"^  - \{fileID: (?:965082891|472722524|583588490)\}\s*$",
                sr_tail,
                re.MULTILINE,
            )
        )
        u6b_count = len(
            re.findall(r"^  - \{fileID: 670270460\}\s*$", sr_tail, re.MULTILINE)
        )
    results["U6"] = (
        f"PASS: 3 true-root fileIDs in m_Roots[]={u6_count} (expected 3)"
        if u6_count == 3
        else f"FAIL: 3 true-root fileIDs in m_Roots[]={u6_count} (expected 3)"
    )
    results["U6b"] = (
        f"PASS: HPBarManager 670270460 in m_Roots[]={u6b_count} (expected 0)"
        if u6b_count == 0
        else f"FAIL: HPBarManager 670270460 in m_Roots[]={u6b_count} (expected 0)"
    )

    # U7: 4 GO definitions appear BEFORE SceneRoots marker line
    # Scan lines until SceneRoots marker; count 4 GO defs
    found = 0
    rooted = False
    for line in post_text.splitlines():
        if line == "--- !u!1660057539 &9223372036854775807":
            rooted = True
            break
        if re.match(r"^--- !u!1 &(472722521|583588488|670270459|965082887)$", line):
            found += 1
    results["U7"] = (
        f"PASS: 4 GO before SceneRoots={found} (expected 4, marker_found={rooted})"
        if (found == 4 and rooted)
        else f"FAIL: 4 GO before SceneRoots={found} (expected 4, marker_found={rooted})"
    )

    # U8: sha256 byte-exact match of extracted dev_tip bundle vs post-splice re-extraction
    # Re-extract bundle from post_text using same algorithm
    post_bundle_parts: list[str] = []
    for _, fid in GO_FILEIDS:
        _, _, go_block = parse_block(post_text, fid)
        comp_ids = parse_component_ids(go_block)
        post_bundle_parts.append(go_block)
        for cid in comp_ids:
            _, _, cblock = find_component_block(post_text, cid)
            post_bundle_parts.append(cblock)
    post_bundle = "".join(post_bundle_parts)
    if not post_bundle.endswith("\n"):
        post_bundle += "\n"
    post_bundle_sha = sha256_bytes(post_bundle.encode("utf-8"))
    results["U8"] = (
        f"PASS: bundle sha256 byte-exact match dev_tip={dev_tip_bundle_sha[:16]}.. == post={post_bundle_sha[:16]}.."
        if post_bundle_sha == dev_tip_bundle_sha
        else f"FAIL: bundle sha256 mismatch dev_tip={dev_tip_bundle_sha[:16]}.. != post={post_bundle_sha[:16]}.."
    )

    return results


def main() -> int:
    # 1. Read dev tip + extract 4 GO bundle
    print("[1/6] Fetching develop tip MVPArena.unity ...", flush=True)
    dev_tip_text = git_show(DEV_TIP_REV, SCENE_FILE_REL)
    print(f"     dev tip line count = {dev_tip_text.count(chr(10))}", flush=True)

    print("[2/6] Building 4 GO bundle from dev tip ...", flush=True)
    bundle, comp_map = build_bundle(dev_tip_text)
    bundle_bytes = bundle.encode("utf-8")
    bundle_sha = sha256_bytes(bundle_bytes)
    bundle_lines = bundle.count("\n")
    print(f"     bundle: lines={bundle_lines} bytes={len(bundle_bytes)} sha256={bundle_sha}", flush=True)
    print(f"     m_Components: {comp_map}", flush=True)
    BUNDLE_DUMP.parent.mkdir(parents=True, exist_ok=True)
    BUNDLE_DUMP.write_bytes(bundle_bytes)

    # 3. Read target file (PR-2 source state)
    print("[3/6] Reading target file (PR-2 source state) ...", flush=True)
    target_text = TARGET_FILE.read_text(encoding="utf-8")
    pre_lines = target_text.count("\n")
    print(f"     pre-splice line count = {pre_lines}", flush=True)

    # 4. Splice at SceneRoots marker
    print("[4/6] Splicing at SceneRoots marker ...", flush=True)
    split_pos = find_sceneroots_split(target_text)
    pre_section = target_text[:split_pos]
    sceneroots_section = target_text[split_pos:]
    print(f"     SceneRoots marker char offset = {split_pos}", flush=True)
    print(f"     pre_section ends with: {repr(pre_section[-50:])}", flush=True)

    # 5. Patch m_Roots[]
    print("[5/6] Patching m_Roots[] (append 3 true-root Transforms) ...", flush=True)
    patched_sceneroots = patch_m_roots(sceneroots_section)
    new_content = pre_section + bundle + patched_sceneroots
    # Write back with LF newlines (binary mode to avoid CRLF on Windows)
    TARGET_FILE.write_bytes(new_content.encode("utf-8"))
    post_lines = new_content.count("\n")
    post_sha = sha256_bytes(new_content.encode("utf-8"))
    print(f"     post-splice line count = {post_lines} (delta = +{post_lines - pre_lines})", flush=True)
    print(f"     post-splice file sha256 = {post_sha}", flush=True)

    # 6. Run U-gates
    print("[6/6] Running U1~U8 unit gates ...", flush=True)
    post_text = TARGET_FILE.read_text(encoding="utf-8")
    results = run_u_gates(post_text, bundle_sha, bundle)
    all_pass = all(v.startswith("PASS") for v in results.values())
    for gate_id, msg in results.items():
        print(f"     {gate_id}: {msg}", flush=True)

    # Write evidence file
    lines: list[str] = []
    lines.append("# A303-314 Phase 2 — Step 3 Evidence Log")
    lines.append("")
    lines.append("## Bundle extraction (from develop tip)")
    lines.append(f"- bundle line count: {bundle_lines}")
    lines.append(f"- bundle byte count: {len(bundle_bytes)}")
    lines.append(f"- bundle sha256: {bundle_sha}")
    lines.append(f"- bundle file: .tmp/phase2-4go-bundle.txt")
    lines.append(f"- m_Components: {comp_map}")
    lines.append("")
    lines.append("## Post-splice MVPArena.unity")
    lines.append(f"- pre-splice line count: {pre_lines}")
    lines.append(f"- post-splice line count: {post_lines}")
    lines.append(f"- delta: +{post_lines - pre_lines}")
    lines.append(f"- post-splice file sha256: {post_sha}")
    lines.append("")
    lines.append("## U-gate results")
    for gate_id, msg in results.items():
        lines.append(f"- {gate_id}: {msg}")
    lines.append("")
    lines.append(f"## Overall: {'ALL PASS' if all_pass else 'FAIL'}")
    EVIDENCE_FILE.write_text("\n".join(lines), encoding="utf-8")

    if not all_pass:
        print("\nFAILED — reverting target file via `git checkout HEAD -- ...`", flush=True)
        subprocess.run(["git", "checkout", "HEAD", "--", SCENE_FILE_REL], cwd=REPO_ROOT, check=False)
        return 1
    print("\nALL U-GATES PASS", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
