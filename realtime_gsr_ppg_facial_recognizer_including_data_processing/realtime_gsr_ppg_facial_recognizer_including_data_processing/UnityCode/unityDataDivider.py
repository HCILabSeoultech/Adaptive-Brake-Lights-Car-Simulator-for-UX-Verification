import os
import re
import sys
import csv
from typing import Dict, List

# 기본 설정
BASE_DIR = os.path.dirname(__file__)
UNITY_DIR = r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_rawdata"
OUTPUT_DIR = r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided"

# 브레이크 유형 표준 라벨 (첫 컬럼 값 그대로 사용)
BRAKE_LABELS = [
    "기본제동등A",
    "밝기변화제동등B",
    "점멸주파수변화제동등C",
    "면적변화제동등D",
]

# 저장 시 라벨 표기 변환 (기본제동등A → A기본제동등 등)
OUTPUT_LABEL_MAP = {
    "기본제동등A": "A기본제동등",
    "밝기변화제동등B": "B밝기변화제동등",
    "점멸주파수변화제동등C": "C점멸주파수변화제동등",
    "면적변화제동등D": "D면적변화제동등",
}

# 파일명 안전 처리
INVALID_CHARS = re.compile(r'[\\/:*?"<>|]+')


def sanitize_filename(name: str) -> str:
    name = INVALID_CHARS.sub("_", name)
    name = name.strip()
    return name if name else "EMPTY"


def detect_encoding(path: str) -> str:
    # 간단 휴리스틱 (BOM 확인)
    with open(path, "rb") as f:
        start = f.read(4)
    if start.startswith(b"\xef\xbb\xbf"):
        return "utf-8-sig"
    return "utf-8"


def parse_filename(fname: str) -> Dict[str, str]:
    """
    기대 패턴: <번호>_<이름>_<성별>_<시나리오>_Unity.csv
    예: 1_윤의진_남자_OneToTwo_Unity.csv
    """
    stem = fname[:-4]
    parts = stem.split("_")
    # 최소 5 파트(번호,이름,성별,시나리오...,Unity)
    if len(parts) < 5 or not parts[-1].lower().endswith("unity"):
        return {}
    user_id = parts[0]
    user_name = parts[1]
    gender = parts[2]
    # "Unity" 앞까지 시나리오 (중간에 _ 포함 가능)
    scenario_parts = parts[3:-1]
    scenario = "_".join(scenario_parts)  # 예: OneToTwo
    return {
        "user_id": user_id,
        "user_name": user_name,
        "gender": gender,
        "scenario": scenario,
    }


def split_unity_file(path: str) -> List[str]:
    """
    1열(브레이크 유형) 값으로 행 분리 → 각 라벨별 파일 생성.
    반환: 생성된 파일 경로 목록
    """
    created = []
    enc = detect_encoding(path)
    with open(path, "r", encoding=enc, newline="") as f:
        reader = csv.reader(f)
        try:
            header = next(reader)
        except StopIteration:
            print(f"[SKIP empty] {os.path.basename(path)}")
            return created

        # 첫 컬럼명 검사
        if not header:
            print(f"[WARN header missing] {os.path.basename(path)}")
            return created

        first_col_index = 0
        rows_by_label = {lbl: [] for lbl in BRAKE_LABELS}

        for row in reader:
            if not row:
                continue
            label_raw = row[first_col_index].strip()
            if label_raw in rows_by_label:
                rows_by_label[label_raw].append(row)
            else:
                # 알 수 없는 라벨은 무시 (필요시 수집해서 보고 가능)
                pass

    meta = parse_filename(os.path.basename(path))
    if not meta:
        print(f"[WARN filename pattern mismatch] {os.path.basename(path)}")
        return created

    for label, rows in rows_by_label.items():
        if not rows:
            continue
        save_label = OUTPUT_LABEL_MAP.get(label, label)
        out_name = f"{meta['user_id']}_{meta['user_name']}_{save_label}_{meta['gender']}_{meta['scenario']}_Unity.csv"
        out_name = sanitize_filename(out_name)
        out_path = os.path.join(OUTPUT_DIR, out_name)
        # utf-8-sig 로 저장 (Excel 호환 필요시)
        with open(out_path, "w", encoding="utf-8-sig", newline="") as wf:
            writer = csv.writer(wf)
            writer.writerow(header)
            writer.writerows(rows)
        created.append(out_path)
        print(f"[OK] {os.path.basename(path)} -> {out_name} ({len(rows)} rows)")
    if not created:
        print(f"[NO MATCH] {os.path.basename(path)} (no known brake labels)")
    return created


def main():
    if not os.path.isdir(UNITY_DIR):
        print("[ERR] Unity_data 폴더 없음:", UNITY_DIR)
        sys.exit(1)

    all_files = sorted(
        f for f in os.listdir(UNITY_DIR)
        if f.lower().endswith(".csv") and "연습" not in f
    )
    if not all_files:
        print("[INFO] 처리 대상 없음 (연습 제외된 csv 미존재)")
        return

    total_created = 0
    for fname in all_files:
        path = os.path.join(UNITY_DIR, fname)
        new_files = split_unity_file(path)
        total_created += len(new_files)

    print(f"[DONE] 생성된 분할 파일 수: {total_created}")


if __name__ == "__main__":
    main()