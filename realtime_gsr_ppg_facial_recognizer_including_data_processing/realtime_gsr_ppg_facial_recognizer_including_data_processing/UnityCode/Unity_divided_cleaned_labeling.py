import re
import shutil
from pathlib import Path

# ===== 설정 =====
SOURCE_DIR = Path(r"c:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided")
OUTPUT_DIR = SOURCE_DIR.parent / "Unity_divided_cleaned_labeled"
DRY_RUN = False      # True: 미리보기, False: 실제 복사
OVERWRITE = False   # True: 동일 파일명 있으면 덮어쓰기

PREFIX_PATTERN = re.compile(r'^P\d+_')      # 이미 라벨링된 파일
FILE_PATTERN = re.compile(r'^(\d+)_([^_]+)_([A-D]).*?\.csv$', re.IGNORECASE)
COND_ORDER = {'A': 0, 'B': 1, 'C': 2, 'D': 3}

def parse_file(file: Path):
    fname = file.name
    if PREFIX_PATTERN.match(fname):
        return None
    m = FILE_PATTERN.match(fname)
    if not m:
        return None
    return {
        'path': file,
        'num': int(m.group(1)),
        'name': m.group(2),
        'cond': m.group(3).upper(),
        'is_cleaned': '_cleaned' in fname.lower()
    }

def collect_files(dir_path: Path):
    out = []
    for f in dir_path.glob("*.csv"):
        info = parse_file(f)
        if info:
            out.append(info)
    return out

def build_groups(entries):
    groups = {}
    for e in entries:
        key = (e['num'], e['cond'])
        groups.setdefault(key, {'cleaned': None, 'raw': None})
        if e['is_cleaned']:
            groups[key]['cleaned'] = e
        else:
            groups[key]['raw'] = e
    return groups

def main():
    if not SOURCE_DIR.is_dir():
        print("SOURCE_DIR 없음:", SOURCE_DIR)
        return
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

    entries = collect_files(SOURCE_DIR)
    if not entries:
        print("대상 파일 없음")
        return

    groups = build_groups(entries)
    ordered_keys = sorted(groups.keys(), key=lambda k: (k[0], COND_ORDER.get(k[1], 99)))

    planned = []  # (src, dst, label, reason)
    p_counter = 1
    for key in ordered_keys:
        num, cond = key
        g = groups[key]
        label = f"P{p_counter}"
        if g['cleaned']:
            target = g['cleaned']; reason = "cleaned 존재 → cleaned 사용"
        elif g['raw']:
            target = g['raw']; reason = "cleaned 없음 → raw 사용"
        else:
            continue
        new_name = f"{label}_{target['path'].name}"
        dst = OUTPUT_DIR / new_name
        if dst.exists() and not OVERWRITE:
            reason += " (이미 존재 → 건너뜀)"
        planned.append((target['path'], dst, label, reason))
        p_counter += 1

    print("=== 복사 예정 목록 ===")
    for src, dst, label, reason in planned:
        action = "SKIP" if (dst.exists() and not OVERWRITE) else "COPY"
        print(f"{label}: {src.name}  ->  {dst.name}  | {action} | {reason}")

    if DRY_RUN:
        print("\nDRY_RUN 모드: 실제 복사 안 함. DRY_RUN=False 로 바꾼 뒤 재실행.")
        return

    copied = skipped = 0
    for src, dst, label, reason in planned:
        if dst.exists() and not OVERWRITE:
            skipped += 1
            continue
        try:
            shutil.copy2(src, dst)
            copied += 1
        except Exception as e:
            print(f"[ERROR] {src.name} -> {dst.name}: {e}")
    print(f"\n완료: 복사 {copied}건, 건너뜀 {skipped}건 (출력 폴더: {OUTPUT_DIR})")

if __name__ == "__main__":
    main()