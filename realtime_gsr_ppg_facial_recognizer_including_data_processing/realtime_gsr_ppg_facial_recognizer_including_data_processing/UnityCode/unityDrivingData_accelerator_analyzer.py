from __future__ import annotations
import pandas as pd
from pathlib import Path
from dataclasses import dataclass, asdict
from typing import List, Dict, Tuple

# 분석 대상 폴더 (이미 가속도 치환 완료된 폴더)
ACCEL_DIR = Path(
    r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel"
)

TIME_COL = "현재 시간"
LEAD_A_COL = "선두 차량 가속도"
BRAKE_COL  = "브레이크 세기"   # 추가

# 구간 정의 (저감속: -4 ~ -1.5, 고감속: <= -5.0)
LOW_DECEL_MIN = -4.0
LOW_DECEL_MAX = -1.5
HIGH_DECEL_MAX = -5.0

# 병합 간격 (초)
MERGE_GAP_SEC = 1.0
MIN_PRINT_DURATION_SEC = 0.8  # 새로 추가: 이 이상 길이만 출력

@dataclass
class Segment:
    seg_type: str
    file: str
    start_idx: int
    end_idx: int
    start_time: str
    end_time: str
    duration_sec: float
    count: int
    min_val: float
    max_val: float
    mean_val: float
    start_sec: float
    end_sec: float
    brake_avg: float = 0.0
    react_time_sec: float = 0.0  # 추가: 첫 비-0 브레이크까지 걸린 시간 (없으면 0)

def parse_time_to_seconds(t: str) -> float:
    # 형식: HH:MM:SS:ms(세 자리)
    try:
        h,m,s,ms = t.split(":")
        return int(h)*3600 + int(m)*60 + int(s) + int(ms)/1000.0
    except Exception:
        return float("nan")

# 단일 파일만 테스트 출력용 경로
TEST_FILE = Path(
    r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel\P4_1_윤의진_D면적변화제동등_남자_OneToTwo_Unity.csv"
)

def extract_segments(values, times_sec, time_strs, file_name: str) -> Tuple[List[Segment], List[Segment]]:
    low_segments: List[Segment] = []
    high_segments: List[Segment] = []

    def flush(start, end, seg_type):
        if start is None or end < start:
            return
        sub_vals = values[start:end+1]
        dur = times_sec[end] - times_sec[start]
        seg = Segment(
            seg_type=seg_type,
            file=file_name,
            start_idx=start,
            end_idx=end,
            start_time=time_strs[start],
            end_time=time_strs[end],
            duration_sec=dur,
            count=len(sub_vals),
            min_val=float(sub_vals.min()),
            max_val=float(sub_vals.max()),
            mean_val=float(sub_vals.mean()),
            start_sec=float(times_sec[start]),
            end_sec=float(times_sec[end])
        )
        if seg_type == "low":
            low_segments.append(seg)
        else:
            high_segments.append(seg)

    n = len(values)

    # low (-2.5 ~ -1.5)
    in_seg = False
    seg_start = None
    for i, v in enumerate(values):
        if LOW_DECEL_MIN <= v <= LOW_DECEL_MAX:
            if not in_seg:
                in_seg = True
                seg_start = i
        else:
            if in_seg:
                flush(seg_start, i-1, "low")
                in_seg = False
    if in_seg:
        flush(seg_start, len(values)-1, "low")

    # high (<= -6.5)
    in_seg = False
    seg_start = None
    for i, v in enumerate(values):
        if v <= HIGH_DECEL_MAX:
            if not in_seg:
                in_seg = True
                seg_start = i
        else:
            if in_seg:
                flush(seg_start, i-1, "high")
                in_seg = False
    if in_seg:
        flush(seg_start, len(values)-1, "high")

    return low_segments, high_segments

def merge_segments_by_time_gap(segs: List[Segment], max_gap: float = MERGE_GAP_SEC) -> List[Segment]:
    if not segs:
        return segs
    segs_sorted = sorted(segs, key=lambda s: s.start_sec)
    merged = []
    cur = segs_sorted[0]
    for nxt in segs_sorted[1:]:
        gap = nxt.start_sec - cur.end_sec
        if gap <= max_gap:
            # 병합
            total_count = cur.count + nxt.count
            mean_val = (cur.mean_val * cur.count + nxt.mean_val * nxt.count) / total_count
            cur = Segment(
                seg_type=cur.seg_type,
                file=cur.file,
                start_idx=cur.start_idx,
                end_idx=nxt.end_idx,
                start_time=cur.start_time,
                end_time=nxt.end_time,
                duration_sec=nxt.end_sec - cur.start_sec,
                count=total_count,
                min_val=min(cur.min_val, nxt.min_val),
                max_val=max(cur.max_val, nxt.max_val),
                mean_val=mean_val,
                start_sec=cur.start_sec,
                end_sec=nxt.end_sec
            )
        else:
            merged.append(cur)
            cur = nxt
    merged.append(cur)
    return merged

def annotate_brake_and_react(segments: List[Segment],
                             times_sec,
                             brake_vals,
                             extend_sec: float = 1.0) -> None:
    """
    각 세그먼트에 대해:
      - brake_avg: [start_sec, end_sec + extend_sec] 구간 내 brake!=0 값 평균
      - react_time_sec: 세그먼트 시작 시각 이후 최초 brake!=0 시각까지 걸린 시간.
                        (구간+extend_sec 내 없다면 0.0, 즉 무반응)
    """
    n = len(times_sec)
    for seg in segments:
        end_plus = seg.end_sec + extend_sec
        vals = []
        first_react_t = None
        # 선형 스캔 (데이터 길이가 크면 이 부분을 이진검색 + 구간 반복으로 최적화 가능)
        for i in range(n):
            t = times_sec[i]
            if t < seg.start_sec:
                continue
            if t > end_plus:
                break
            b = brake_vals[i]
            if b != 0:
                vals.append(b)
                if first_react_t is None:
                    first_react_t = t
        seg.brake_avg = float(sum(vals)/len(vals)) if vals else 0.0
        if first_react_t is not None:
            seg.react_time_sec = max(0.0, first_react_t - seg.start_sec)
        else:
            seg.react_time_sec = 0.0

def analyze_file(path: Path) -> Dict[str, List[Dict]]:
    try:
        df_local = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df_local = pd.read_csv(path, encoding="cp949")

    if LEAD_A_COL not in df_local.columns or TIME_COL not in df_local.columns or BRAKE_COL not in df_local.columns:
        return {"file": path.name, "low": [], "high": []}

    values = df_local[LEAD_A_COL].astype(float).to_numpy()
    times_sec = df_local[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df_local[TIME_COL].tolist()
    brake_vals = df_local[BRAKE_COL].astype(float).to_numpy()

    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs)
    high_segs = merge_segments_by_time_gap(high_segs)

    annotate_brake_and_react(low_segs, times_sec, brake_vals, extend_sec=1.0)
    annotate_brake_and_react(high_segs, times_sec, brake_vals, extend_sec=1.0)

    return {
        "file": path.name,
        "low": [asdict(s) for s in low_segs],
        "high": [asdict(s) for s in high_segs],
    }

def analyze_single_file(path: Path):
    try:
        df_local = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df_local = pd.read_csv(path, encoding="cp949")

    if LEAD_A_COL not in df_local.columns or TIME_COL not in df_local.columns or BRAKE_COL not in df_local.columns:
        print("필수 컬럼 없음")
        return

    vals = df_local[LEAD_A_COL].astype(float).to_numpy()
    times_sec = df_local[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df_local[TIME_COL].tolist()
    brake_vals = df_local[BRAKE_COL].astype(float).to_numpy()

    low_segs, high_segs = extract_segments(vals, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs)
    high_segs = merge_segments_by_time_gap(high_segs)

    annotate_brake_and_react(low_segs, times_sec, brake_vals, extend_sec=1.0)
    annotate_brake_and_react(high_segs, times_sec, brake_vals, extend_sec=1.0)

    # 필터 적용 세그먼트
    filtered_low  = [s for s in low_segs  if s.duration_sec >= MIN_PRINT_DURATION_SEC]
    filtered_high = [s for s in high_segs if s.duration_sec >= MIN_PRINT_DURATION_SEC]

    print(f"[파일] {path.name}")
    print(f"저감속(low) 전체 {len(low_segs)}개 / 출력(>= {MIN_PRINT_DURATION_SEC}s) {len(filtered_low)}개")
    print(f"고감속(high) 전체 {len(high_segs)}개 / 출력(>= {MIN_PRINT_DURATION_SEC}s) {len(filtered_high)}개")
    print(f"brake_avg: 각 구간 +1s 내 brake!=0 평균\n")

    def print_segs(title, segs):
        for s in segs[:10]:
            print(f"{s.seg_type} idx[{s.start_idx}-{s.end_idx}] "
                  f"time {s.start_time}->{s.end_time} dur={s.duration_sec:.3f}s "
                  f"n={s.count} min={s.min_val:.3f} max={s.max_val:.3f} mean={s.mean_val:.3f} "
                  f"brake_avg={s.brake_avg:.3f}")
        if len(segs) > 10:
            print(f"... ({len(segs)-10} 더 있음)")
        print()

    print("--- 저감속(low) (필터 후) ---")
    print_segs("low", filtered_low)
    print("--- 고감속(high) (필터 후) ---")
    print_segs("high", filtered_high)

    # 배열 출력: 전체 vs 필터 후
    low_brakes_all       = [round(s.brake_avg, 5) for s in low_segs]
    low_brakes_filtered  = [round(s.brake_avg, 5) for s in filtered_low]
    high_brakes_all      = [round(s.brake_avg, 5) for s in high_segs]
    high_brakes_filtered = [round(s.brake_avg, 5) for s in filtered_high]

    print(f"low brake_avg 전체({len(low_brakes_all)}): {low_brakes_all}")
    print(f"low brake_avg 필터({len(low_brakes_filtered)}): {low_brakes_filtered} avg:{sum(low_brakes_filtered)/len(low_brakes_filtered) if low_brakes_filtered else 0}")
    # 추가: 필터된 low 구간 중 brake_avg == 0 개수와 0이 아닌 값들의 평균
    zero_cnt = sum(1 for v in low_brakes_filtered if v == 0)
    nonzero_vals = [v for v in low_brakes_filtered if v != 0]
    nonzero_mean = (sum(nonzero_vals)/len(nonzero_vals)) if nonzero_vals else 0.0
    print(f"low brake_avg (필터) 0값 개수:{zero_cnt}  0이 아닌 값 평균:{nonzero_mean:.3f}")
    
    print(f"high brake_avg 전체({len(high_brakes_all)}): {high_brakes_all}")
    print(f"high brake_avg 필터({len(high_brakes_filtered)}): {high_brakes_filtered} avg:{sum(high_brakes_filtered)/len(high_brakes_filtered) if high_brakes_filtered else 0}")
    zero_cnt = sum(1 for v in high_brakes_filtered if v == 0)
    nonzero_vals = [v for v in high_brakes_filtered if v != 0]
    nonzero_mean = (sum(nonzero_vals)/len(nonzero_vals)) if nonzero_vals else 0.0
    print(f"high brake_avg (필터) 0값 개수:{zero_cnt}  0이 아닌 값 평균:{nonzero_mean:.3f}")
    
    # 반응시간 배열 (필터 후)
    low_react_times_filtered  = [round(s.react_time_sec, 3) for s in filtered_low]
    high_react_times_filtered = [round(s.react_time_sec, 3) for s in filtered_high]
    print(f"low react_time 배열(필터, s): {low_react_times_filtered}")
    print(f"high react_time 배열(필터, s): {high_react_times_filtered}")

def main():
    results = []
    for csv_file in sorted(ACCEL_DIR.glob("*.csv")):
        res = analyze_file(csv_file)
        results.append(res)
        # 간단 출력
        print(f"[{csv_file.name}] low:{len(res['low'])}  high:{len(res['high'])}")

    # 상세 예시 출력 (첫 파일)
    if results:
        first = results[0]
        print("\n--- 첫 파일 상세 (low) ---")
        for seg in first["low"][:5]:
            print(seg)
        print("\n--- 첫 파일 상세 (high) ---")
        for seg in first["high"][:5]:
            print(seg)

def get_brake_stats(path: Path,
                    duration_threshold: float = MIN_PRINT_DURATION_SEC,
                    extend_sec: float = 1.0
                    ) -> Tuple[int, int, float, int, int, float, float, float]:
    """
    반환:
      (low_cnt, low_zero_cnt, low_nonzero_mean,
       high_cnt, high_zero_cnt, high_nonzero_mean,
       low_react_nonzero_mean, high_react_nonzero_mean)

    react_time 평균은 0이 아닌(반응 발생) 세그먼트만 대상으로 함. 반응 없으면 0.0
    """
    try:
        df_local = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df_local = pd.read_csv(path, encoding="cp949")

    required = {LEAD_A_COL, TIME_COL, BRAKE_COL}
    if not required.issubset(df_local.columns):
        return (0,0,0.0, 0,0,0.0, 0.0, 0.0)

    values = df_local[LEAD_A_COL].astype(float).to_numpy()
    times_sec = df_local[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df_local[TIME_COL].tolist()
    brake_vals = df_local[BRAKE_COL].astype(float).to_numpy()

    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs)
    high_segs = merge_segments_by_time_gap(high_segs)

    annotate_brake_and_react(low_segs, times_sec, brake_vals, extend_sec=extend_sec)
    annotate_brake_and_react(high_segs, times_sec, brake_vals, extend_sec=extend_sec)

    filtered_low  = [s for s in low_segs  if s.duration_sec >= duration_threshold]
    filtered_high = [s for s in high_segs if s.duration_sec >= duration_threshold]

    def zero_and_nonzero_mean(arr: List[float]) -> Tuple[int, float]:
        zero_cnt = sum(1 for v in arr if v == 0)
        nz = [v for v in arr if v != 0]
        nz_mean = sum(nz)/len(nz) if nz else 0.0
        return zero_cnt, nz_mean

    low_brakes  = [s.brake_avg for s in filtered_low]
    high_brakes = [s.brake_avg for s in filtered_high]

    low_zero_cnt, low_nz_mean   = zero_and_nonzero_mean(low_brakes)
    high_zero_cnt, high_nz_mean = zero_and_nonzero_mean(high_brakes)

    low_reacts_list  = [s.react_time_sec for s in filtered_low]
    high_reacts_list = [s.react_time_sec for s in filtered_high]

    # 0 제외한 반응시간 평균
    low_react_nonzero = [v for v in low_reacts_list if v > 0]
    high_react_nonzero = [v for v in high_reacts_list if v > 0]
    low_react_nonzero_mean  = float(sum(low_react_nonzero)/len(low_react_nonzero) if low_react_nonzero else 0.0)
    high_react_nonzero_mean = float(sum(high_react_nonzero)/len(high_react_nonzero) if high_react_nonzero else 0.0)

    return (len(filtered_low), low_zero_cnt, low_nz_mean, low_react_nonzero_mean,
            len(filtered_high), high_zero_cnt, high_nz_mean, high_react_nonzero_mean)

# __main__ 예시 출력 갱신
if __name__ == "__main__":
    stats = get_brake_stats(TEST_FILE)
    print("브레이크/반응 통계 (low_cnt, low_zero_cnt, low_nonzero_brake_mean, low_react_nonzero_mean, "
          "high_cnt, high_zero_cnt, high_nonzero_brake_mean, high_react_nonzero_mean):")
    print(stats)
    analyze_single_file(TEST_FILE)
