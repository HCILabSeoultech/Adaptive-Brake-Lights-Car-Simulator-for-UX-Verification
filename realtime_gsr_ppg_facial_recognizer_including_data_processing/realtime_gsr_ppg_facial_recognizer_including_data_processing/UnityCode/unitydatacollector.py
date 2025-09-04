from __future__ import annotations
import sys
from pathlib import Path
from unityDrivingData_accelerator_analyzer import get_brake_stats
from unityDrivingDatapPedalAnalyzer import _get_brake_pedal_stats, _get_accel_pedal_stats
from unityDrivingDataTimeAnalyzer import get_total_and_per_km_time
import re
import csv
# unitydatacollector.py
# unityDrivingData_accelerator_analyzer.py 안의 get_brake_stats(TEST_FILE) 호출 예시


# 1) unityDrivingData_accelerator_analyzer.py 위치 설정
# 이 파일(unitydatacollector.py)이 analyzer 파일과 같은 폴더라면 아래 두 줄은 불필요.
# 다른 위치라면 상대/절대 경로 맞게 수정.
PROJECT_ROOT = Path(r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel")
FOLDER_DIR = Path(r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel")
ANALYZER_DIR = PROJECT_ROOT  # 같은 폴더라면 그대로
# ANALYZER_DIR = PROJECT_ROOT / "subdir"  # 폴더 다르면 예: 수정
if str(ANALYZER_DIR) not in sys.path:
    sys.path.insert(0, str(ANALYZER_DIR))


def run_brake_stats(test_file: str | Path):
    test_file = Path(test_file)
    if not test_file.is_file():
        raise FileNotFoundError(f"데이터 파일 없음: {test_file}")
    stats = get_brake_stats(test_file)
    # print("Brake stats:", stats)
    return stats

def run_brake_pedal_stats(test_file: str | Path):
    test_file = Path(test_file)
    if not test_file.is_file():
        raise FileNotFoundError(f"데이터 파일 없음: {test_file}")
    stats = _get_brake_pedal_stats(test_file)
    # print("Brake pedal stats:", stats)
    return stats
def run_accel_pedal_stats(test_file: str | Path):
    test_file = Path(test_file)
    if not test_file.is_file():
        raise FileNotFoundError(f"데이터 파일 없음: {test_file}")
    stats = _get_accel_pedal_stats(test_file)
    # print("Accel pedal stats:", stats)
    return stats

def run_total_and_per_km_time(file_path: str | Path):
    file_path = Path(file_path)
    if not file_path.is_file():
        raise FileNotFoundError(f"데이터 파일 없음: {file_path}")
    stats = get_total_and_per_km_time(file_path)
    # print("Total and per km time stats:", stats)
    return stats

def _parse_file_info(p: Path):
    """
    파일명 패턴: P<번호>_<이름>_<제동등타입>_<성별>_...
    반환: (실험자번호, 실험번호, 제동등타입)
      실험번호 = P 뒤 숫자
      실험자번호 = (실험번호-1)//4 + 1   (P1~4 ->1, P5~8 ->2, P9~12 ->3 ...)
    """
    stem = p.stem  # 확장자 제거
    parts = stem.split("_")
    if len(parts) < 3:
        raise ValueError(f"파일명 형태 예상과 다름: {p.name}")
    m = re.match(r"P(\d+)", parts[0])
    if not m:
        raise ValueError(f"P<number> 패턴 없음: {p.name}")
    exp_num = int(m.group(1))             # 실험번호
    participant_num = (exp_num - 1)//4 + 1 # 실험자번호
    brake_type = parts[3]                 # 제동등타입
    return participant_num, exp_num, brake_type

HEADER = [
    "participant_id","experiment_id","brake_light_type",
    "total_seconds","per_km",
    "low_cnt","low_zero_cnt","low_nonzero_brake_mean","low_react_nonzero_mean",
    "high_cnt","high_zero_cnt","high_nonzero_brake_mean","high_react_nonzero_mean",
    "brake_count","brake_min_dur","brake_max_dur","brake_avg_dur","brake_std_dur",
    "brake_min_max","brake_max_max","brake_avg_max","brake_std_max",
    "accel_count","accel_min_dur","accel_max_dur","accel_avg_dur","accel_std_dur",
    "accel_min_max","accel_max_max","accel_avg_max","accel_std_max"
]

def run_all(file_path: str | Path):
    file_path = Path(file_path)
    if not file_path.is_file():
        raise FileNotFoundError(f"데이터 파일 없음: {file_path}")
    total_and_per_km_time = run_total_and_per_km_time(file_path)      # (total_seconds, per_km)
    brake_stats = run_brake_stats(file_path)                          # (low_cnt, low_zero_cnt, low_nonzero_mean, low_react_nonzero_mean, high_cnt, high_zero_cnt, high_nonzero_mean, high_react_nonzero_mean)
    brake_pedal_stats = run_brake_pedal_stats(file_path)              # dict -> brake_*
    accel_pedal_stats = run_accel_pedal_stats(file_path)              # dict -> accel_*

    key_order = ["count","min_dur","max_dur","avg_dur","std_dur","min_max","max_max","avg_max","std_max"]

    participant_num, exp_num, brake_type = _parse_file_info(file_path)

    flat_values = []
    flat_values.extend([participant_num, exp_num, brake_type])
    flat_values.extend(total_and_per_km_time)
    flat_values.extend(brake_stats)
    flat_values.extend(brake_pedal_stats.get(k, "") for k in key_order)
    flat_values.extend(accel_pedal_stats.get(k, "") for k in key_order)

    return list(flat_values)

def write_csv_for_folder(output_csv: str | Path, folder: Path = FOLDER_DIR):
    folder = Path(folder)
    rows = []
    for fp in sorted(folder.glob("*.csv")):
        try:
            row = run_all(fp)
            rows.append(row)
        except Exception as e:
            print(f"Skip {fp.name}: {e}")

    # experiment_id (열 인덱스 1) 기준 오름차순 정렬
    rows.sort(key=lambda r: r[1])

    out_path = Path(output_csv)
    with out_path.open("w", newline="", encoding="utf-8-sig") as f:
        writer = csv.writer(f)
        writer.writerow(HEADER)
        writer.writerows(rows)
    print(f"작성 완료: {out_path} (rows={len(rows)})")

if __name__ == "__main__":
    # 폴더 전체 처리하여 한 CSV 생성 (원하면 파일명 변경)
    write_csv_for_folder(PROJECT_ROOT / "unity_aggregated_metrics.csv")
