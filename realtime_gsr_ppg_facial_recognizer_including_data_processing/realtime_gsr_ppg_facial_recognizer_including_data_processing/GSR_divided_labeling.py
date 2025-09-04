import re
import shutil
from pathlib import Path

# 설정
SOURCE_DIR = Path(r"c:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\Processed_GSR")
OUTPUT_DIR = Path(r"c:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\Processed_GSR_labeled")
DRY_RUN = False      # True: 미리보기만, False: 실제 복사
OVERWRITE = False   # 동일 이름 존재시 덮어쓰기 여부

# 패턴
PREFIX_PATTERN = re.compile(r'^P\d+_')
FILE_PATTERN = re.compile(r'^(\d+)_([^_]+)_([A-D])\.csv$', re.IGNORECASE)
COND_ORDER = {'A': 0, 'B': 1, 'C': 2, 'D': 3}

def parse_file(path: Path):
    name = path.name
    if PREFIX_PATTERN.match(name):
        return None  # 이미 처리됨
    m = FILE_PATTERN.match(name)
    if not m:
        return None
    return {
        "path": path,
        "num": int(m.group(1)),
        "user": m.group(2),
        "cond": m.group(3).upper()
    }

def collect_entries():
    entries = []
    if not SOURCE_DIR.is_dir():
        print(f"[ERR] SOURCE_DIR 없음: {SOURCE_DIR}")
        return entries
    for f in SOURCE_DIR.glob("*.csv"):
        info = parse_file(f)
        if info:
            entries.append(info)
    return entries

def plan_labeling(entries):
    # (num, cond) 순 정렬 → 1_A,1_B,1_C,1_D,2_A,...
    entries.sort(key=lambda e: (e["num"], COND_ORDER.get(e["cond"], 99)))
    plan = []
    p_counter = 1
    for e in entries:
        label = f"P{p_counter}"
        new_name = f"{label}_{e['path'].name}"
        dst = OUTPUT_DIR / new_name
        reason = ""
        if dst.exists() and not OVERWRITE:
            reason = "이미 존재 → 건너뜀"
        plan.append((e["path"], dst, label, reason))
        p_counter += 1
    return plan

def execute(plan):
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    copied = skipped = 0
    for src, dst, label, reason in plan:
        if dst.exists() and not OVERWRITE:
            print(f"[SKIP] {src.name} -> {dst.name} | {reason}")
            skipped += 1
            continue
        try:
            shutil.copy2(src, dst)
            print(f"[COPY] {src.name} -> {dst.name}")
            copied += 1
        except Exception as e:
            print(f"[ERR ] {src.name} -> {dst.name}: {e}")
    print(f"[DONE] 복사 {copied}건, 건너뜀 {skipped}건 (출력: {OUTPUT_DIR})")

def main():
    entries = collect_entries()
    if not entries:
        print("[INFO] 처리 대상 없음")
        return
    plan = plan_labeling(entries)
    print("=== 계획 ===")
    for src, dst, label, reason in plan:
        action = "SKIP" if reason else "COPY"
        print(f"{label}: {src.name} -> {dst.name} | {action} {reason}")
    if DRY_RUN:
        print("\nDRY_RUN=True: 실제 복사하지 않음. 실행하려면 DRY_RUN=False 로 변경.")
        return
    execute(plan)

if __name__ == "__main__":
    main()