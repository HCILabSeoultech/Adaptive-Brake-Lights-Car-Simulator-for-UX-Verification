import pandas as pd
from pathlib import Path
from typing import Union, Tuple

FILE_PATH = r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled\P43_11_박홍인_C점멸주파수변화제동등_남자_OneToTwo_Unity.csv"
TIME_COL = "현재 시간"

def parse_unity_time(t: str) -> pd.Timedelta:
    h, m, s, ms = map(int, str(t).split(":"))
    return pd.Timedelta(hours=h, minutes=m, seconds=s, milliseconds=ms)

def get_total_and_per_km_time(file_path: Union[str, Path],
                              course_km: float = 6.0
                              ) -> Tuple[float, float]:
    """
    file_path 를 받아 전체 주행 시간(초)과 (전체시간 / course_km) (초)을 소수 3자리 반올림하여 반환.
    반환: (total_seconds_3dec, per_km_seconds_3dec)
    """
    file_path = Path(file_path)
    df = pd.read_csv(file_path, encoding="utf-8-sig")
    if TIME_COL not in df.columns:
        raise KeyError(f"{TIME_COL} 열 없음: {df.columns.tolist()}")

    start_td = parse_unity_time(df[TIME_COL].iloc[0])
    end_td = parse_unity_time(df[TIME_COL].iloc[-1])
    if end_td < start_td:  # 자정 넘어간 경우
        end_td += pd.Timedelta(days=1)

    duration = end_td - start_td
    total_seconds = duration.total_seconds()
    per_km = total_seconds / course_km if course_km else 0.0
    return (round(total_seconds, 3), round(per_km, 3))

def main():
    total_sec, per_km = get_total_and_per_km_time(FILE_PATH)
    print(f"총 경과(초): {total_sec:.3f}")
    print(f"1km 당 평균 주파시간: {per_km:.3f}초")

if __name__ == "__main__":
    main()