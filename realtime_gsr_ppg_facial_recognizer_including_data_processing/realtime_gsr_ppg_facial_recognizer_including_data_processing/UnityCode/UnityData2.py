from __future__ import annotations
import pandas as pd
import numpy as np
from dataclasses import dataclass
from pathlib import Path
from typing import List, Tuple
import sys

# 데이터 폴더
ACCEL_DIR = Path(
    r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel"
)

# 컬럼명
TIME_COL = "현재 시간"
LEAD_A_COL = "선두 차량 가속도"
BRAKE_COL  = "브레이크 세기"
ACCEL_PEDAL_COL = "엑셀 세기"
FRONT_GAP_COL = "실시간 앞차 간격"  # 선두 차량과의 거리(간격) 컬럼
# 저감속/고감속 기준 (unityDrivingData_accelerator_analyzer.py와 동일)
LOW_DECEL_MIN = -4.0
LOW_DECEL_MAX = -1.5
HIGH_DECEL_MAX = -5.0

# 인접 구간 병합 기준(초)
MERGE_GAP_SEC = 1.0
# 최소 구간 길이(초): 이 값 미만은 구간으로 판단하지 않음
MIN_DURATION_SEC = 0.8

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

def parse_time_to_seconds(t: str) -> float:
    # "HH:MM:SS:ms" -> 초
    try:
        h, m, s, ms = t.split(":")
        return int(h) * 3600 + int(m) * 60 + int(s) + int(ms) / 1000.0
    except Exception:
        return float("nan")

def extract_segments(values, times_sec, time_strs, file_name: str) -> Tuple[List[Segment], List[Segment]]:
    low_segments: List[Segment] = []
    high_segments: List[Segment] = []

    def flush(start, end, seg_type):
        if start is None or end < start:
            return
        sub_vals = values[start:end+1]
        dur = float(times_sec[end] - times_sec[start])
        seg = Segment(
            seg_type=seg_type,
            file=file_name,
            start_idx=start,
            end_idx=end,
            start_time=str(time_strs[start]),
            end_time=str(time_strs[end]),
            duration_sec=dur,
            count=int(len(sub_vals)),
            min_val=float(sub_vals.min()),
            max_val=float(sub_vals.max()),
            mean_val=float(sub_vals.mean()),
            start_sec=float(times_sec[start]),
            end_sec=float(times_sec[end]),
        )
        if seg_type == "low":
            low_segments.append(seg)
        else:
            high_segments.append(seg)

    n = len(values)

    # 저감속: [-4.0, -1.5]
    in_seg = False
    seg_start = None
    for i, v in enumerate(values):
        if LOW_DECEL_MIN <= v <= LOW_DECEL_MAX:
            if not in_seg:
                in_seg = True
                seg_start = i
        else:
            if in_seg:
                flush(seg_start, i - 1, "low")
                in_seg = False
    if in_seg:
        flush(seg_start, n - 1, "low")

    # 고감속: <= -5.0
    in_seg = False
    seg_start = None
    for i, v in enumerate(values):
        if v <= HIGH_DECEL_MAX:
            if not in_seg:
                in_seg = True
                seg_start = i
        else:
            if in_seg:
                flush(seg_start, i - 1, "high")
                in_seg = False
    if in_seg:
        flush(seg_start, n - 1, "high")

    return low_segments, high_segments

def merge_segments_by_time_gap(segs: List[Segment], max_gap: float = MERGE_GAP_SEC) -> List[Segment]:
    if not segs:
        return segs
    segs_sorted = sorted(segs, key=lambda s: s.start_sec)
    merged: List[Segment] = []
    cur = segs_sorted[0]
    for nxt in segs_sorted[1:]:
        gap = nxt.start_sec - cur.end_sec
        if gap <= max_gap:
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
                end_sec=nxt.end_sec,
            )
        else:
            merged.append(cur)
            cur = nxt
    merged.append(cur)
    return merged

def print_segments(title: str, segs: List[Segment], limit: int | None = None) -> None:
    print(f"--- {title} ({len(segs)}개) ---")
    shown = segs if limit is None else segs[:limit]
    for s in shown:
        print(
            f"{s.seg_type} idx[{s.start_idx}-{s.end_idx}] "
            f"time {s.start_time}->{s.end_time} "
            f"dur={s.duration_sec:.3f}s n={s.count} "
            f"min={s.min_val:.3f} max={s.max_val:.3f} mean={s.mean_val:.3f}"
        )
    if limit is not None and len(segs) > limit:
        print(f"... ({len(segs) - limit}개 더 있음)")
    print()

def print_low_segments_with_brake(title: str, segs: List[Segment], brake_vals, times_sec, accel_vals=None, limit: int | None = None) -> None:
    print(f"--- {title} ({len(segs)}개) ---")
    # 전체 세그먼트 기준으로 배열 계산
    all_results = []
    for s in segs:
        # 평균 브레이크 세기(구간 내)
        seg_avg = float(brake_vals[s.start_idx:s.end_idx+1].mean()) if s.end_idx >= s.start_idx else 0.0

        # 반응시간 1: brake>0 시점
        rt_brake = None
        for i in range(s.start_idx, s.end_idx + 1):
            if brake_vals[i] > 0:
                rt_brake = float(times_sec[i] - s.start_sec)
                break

        # 반응시간 2: 엑셀 시작값 대비 0.2 이상 하강
        rt_accel = None
        if accel_vals is not None and s.end_idx >= s.start_idx:
            start_val = float(accel_vals[s.start_idx])
            if start_val > 0.0:
                target_drop = 0.2
                for i in range(s.start_idx, s.end_idx + 1):
                    if start_val - float(accel_vals[i]) >= target_drop:
                        rt_accel = float(times_sec[i] - s.start_sec)
                        break

        # 토탈 반응시간: 둘 중 작은 값, 둘 다 없음이면 0.0
        if rt_brake is None and rt_accel is None:
            total_rt = 0.0
        elif rt_brake is None:
            total_rt = rt_accel
        elif rt_accel is None:
            total_rt = rt_brake
        else:
            total_rt = min(rt_brake, rt_accel)

        all_results.append((s, seg_avg, rt_brake, rt_accel, total_rt))

    # 행 출력은 limit까지
    shown = all_results if limit is None else all_results[:limit]
    for s, seg_avg, rt_brake, rt_accel, total_rt in shown:
        print(
            f"{s.seg_type} idx[{s.start_idx}-{s.end_idx}] "
            f"time {s.start_time}->{s.end_time} "
            f"dur={s.duration_sec:.3f}s n={s.count} "
            f"min={s.min_val:.3f} max={s.max_val:.3f} mean={s.mean_val:.3f} "
            f"brake_avg={seg_avg:.5f} "
            f"brake_rt={('X' if rt_brake is None else f'{rt_brake:.3f}s')} "
            f"accel_rt={('X' if rt_accel is None else f'{rt_accel:.3f}s')} "
            f"total_rt={total_rt:.3f}s"
        )
    if limit is not None and len(segs) > limit:
        print(f"... ({len(segs) - (0 if limit is None else limit)}개 더 있음)")

    # 배열 출력(전체 세그먼트 기준)
    def fmt_arr(arr):
        return [("X" if v is None else round(v, 3)) for v in arr]
    per_seg_brake_avg = [seg_avg for (_, seg_avg, _, _, _) in all_results]
    per_seg_brake_rt  = [rtb for (_, _, rtb, _, _) in all_results]
    per_seg_accel_rt  = [rta for (_, _, _, rta, _) in all_results]
    per_seg_total_rt  = [tot for (_, _, _, _, tot) in all_results]

    print(f"저감속 brake 세기 배열({len(per_seg_brake_avg)}개): {[round(v,5) for v in per_seg_brake_avg]}")
    print(f"저감속 반응시간(브레이크>0) 배열({len(per_seg_brake_rt)}개): {fmt_arr(per_seg_brake_rt)}")
    print(f"저감속 반응시간(엑셀 -0.2) 배열({len(per_seg_accel_rt)}개): {fmt_arr(per_seg_accel_rt)}")
    print(f"저감속 반응시간(토탈) 배열({len(per_seg_total_rt)}개): {[round(v,3) for v in per_seg_total_rt]}")
    print()

def print_high_segments_min_gap(title: str, segs: List[Segment], gap_vals, limit: int | None = None) -> None:
    """
    고감속 구간별로 '실시간 앞차 간격' 최소값을 계산해 배열로 출력.
    """
    print(f"--- {title} ({len(segs)}개) ---")
    min_gaps = []
    for s in segs:
        sl = gap_vals[s.start_idx:s.end_idx+1]
        if len(sl) == 0:
            min_gaps.append(float("nan"))
        else:
            # NaN 무시하고 최소값 계산
            mg = float(np.nanmin(sl))
            min_gaps.append(mg)
    print(f"고감속 선두차 거리 최소 배열({len(min_gaps)}개): {[round(v,5) if pd.notna(v) else 'NaN' for v in min_gaps]}")
    print()

def print_summary_arrays_one_line(
    low_segs: List[Segment],
    high_segs: List[Segment],
    brake_vals,
    times_sec,
    accel_vals,
    gap_vals
) -> None:
    # 저감속 brake 평균
    low_brake_avgs = []
    # 저감속 토탈 반응시간
    low_total_rts = []

    for s in low_segs:
        # 평균 브레이크 세기
        if s.end_idx >= s.start_idx:
            seg_brake_slice = brake_vals[s.start_idx:s.end_idx+1]
            avg_b = float(np.nanmean(seg_brake_slice)) if len(seg_brake_slice) else 0.0
        else:
            avg_b = 0.0
        low_brake_avgs.append(avg_b)

        # 반응시간 계산
        rt_brake = None
        for i in range(s.start_idx, s.end_idx + 1):
            if brake_vals[i] > 0:
                rt_brake = float(times_sec[i] - s.start_sec)
                break

        rt_accel = None
        if accel_vals is not None and s.end_idx >= s.start_idx:
            start_val = float(accel_vals[s.start_idx])
            if start_val > 0.0:
                for i in range(s.start_idx, s.end_idx + 1):
                    if start_val - float(accel_vals[i]) >= 0.2:
                        rt_accel = float(times_sec[i] - s.start_sec)
                        break

        if rt_brake is None and rt_accel is None:
            total_rt = 0.0
        elif rt_brake is None:
            total_rt = rt_accel
        elif rt_accel is None:
            total_rt = rt_brake
        else:
            total_rt = min(rt_brake, rt_accel)
        low_total_rts.append(total_rt)

    # 고감속 구간별 선두차 최소 거리
    high_min_gaps = []
    if gap_vals is not None:
        for s in high_segs:
            sl = gap_vals[s.start_idx:s.end_idx+1]
            if len(sl) == 0:
                high_min_gaps.append(float("nan"))
            else:
                high_min_gaps.append(float(np.nanmin(sl)))

    print(f"저감속 brake 세기 배열({len(low_brake_avgs)}개): {[round(v,5) for v in low_brake_avgs]}")
    print(f"저감속 반응시간(토탈) 배열({len(low_total_rts)}개): {[round(v,3) for v in low_total_rts]}")
    print(f"고감속 선두차 거리 최소 배열({len(high_min_gaps)}개): {[round(v,5) if pd.notna(v) else 'NaN' for v in high_min_gaps]}")
    print()

# 새로 추가: 파일명에서 메타 파싱
def parse_meta_from_filename(filename: str) -> Tuple[str, str]:
    """
    <실험번호>_<참여자번호>_<참여자이름>_<브레이크유형>_<성별>_<운전경력>[_Unity].csv
    예: P100_25_정영운_D면적변화제동등_남자_OverTwo_Unity.csv
    반환: (참여자번호, 브레이크유형). 파싱 실패 시 공백 반환.
    """
    stem = Path(filename).stem
    parts = stem.split("_")
    if len(parts) >= 4:
        return parts[1], parts[3]
    return "", ""

# 새로 추가: 저감속 세그먼트 보고서 DataFrame 생성
def build_low_segments_report_df(
    file_name: str,
    low_segs: List[Segment],
    brake_vals: np.ndarray,
    times_sec: np.ndarray,
    accel_vals: np.ndarray | None
) -> pd.DataFrame:
    pid, brake_type = parse_meta_from_filename(file_name)

    rows = []
    for idx, s in enumerate(low_segs):  # seg index -> 1부터
        seg_no = idx + 1

        # 평균 브레이크 세기
        if s.end_idx >= s.start_idx:
            seg_slice = brake_vals[s.start_idx:s.end_idx+1]
            avg_brake = float(np.nanmean(seg_slice)) if len(seg_slice) else 0.0
        else:
            avg_brake = 0.0

        # 반응시간(브레이크)
        rt_brake = None
        for i in range(s.start_idx, s.end_idx + 1):
            if brake_vals[i] > 0:
                rt_brake = float(times_sec[i] - s.start_sec)
                break

        # 반응시간(엑셀 -0.2)
        rt_accel = None
        if accel_vals is not None and s.end_idx >= s.start_idx:
            start_val = float(accel_vals[s.start_idx])
            if start_val > 0.0:
                for i in range(s.start_idx, s.end_idx + 1):
                    if start_val - float(accel_vals[i]) >= 0.2:
                        rt_accel = float(times_sec[i] - s.start_sec)
                        break

        # 토탈 반응시간
        if rt_brake is None and rt_accel is None:
            total_rt = 0.0
        elif rt_brake is None:
            total_rt = rt_accel
        elif rt_accel is None:
            total_rt = rt_brake
        else:
            total_rt = min(rt_brake, rt_accel)

        rows.append({
            "참여자번호": pid,
            "브레이크유형": brake_type,
            "저감속구간 번호": seg_no,                  # ← 추가(3번째 컬럼)
            "저감속구간의 길이": round(s.duration_sec, 3),
            "브레이크세기": round(avg_brake, 5),
            "반응시간": round(float(total_rt), 3),
        })

    return pd.DataFrame(rows)

# 고감속 세그먼트 보고서 DataFrame 생성(최소거리 = '실시간 앞차 간격' 최소값)
def build_high_segments_report_df(
    file_name: str,
    high_segs: List[Segment],
    gap_vals: np.ndarray | None
) -> pd.DataFrame:
    pid, brake_type = parse_meta_from_filename(file_name)
    rows = []
    for idx, s in enumerate(high_segs):
        seg_no = idx + 1
        if gap_vals is not None and s.end_idx >= s.start_idx:
            sl = gap_vals[s.start_idx:s.end_idx+1]
            if len(sl) == 0 or np.isnan(sl).all():
                min_gap = np.nan
            else:
                # 이 계산에서의 선두차 거리 최소 배열과 동일하게 계산
                min_gap = float(np.nanmin(sl))
        else:
            min_gap = np.nan

        rows.append({
            "참여자번호": pid,
            "브레이크유형": brake_type,
            "고감속구간 번호": seg_no,   # ← 추가(3번째 컬럼)
            "고감속구간길이": round(s.duration_sec, 3),
            "최소거리": (np.nan if pd.isna(min_gap) else round(min_gap, 5)),
        })
    return pd.DataFrame(rows)

def load_csv_with_fallback(path: Path) -> pd.DataFrame:
    try:
        return pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        return pd.read_csv(path, encoding="cp949")

def process_file(path: Path) -> None:
    print(f"\n[파일] {path.name}")
    try:
        df = load_csv_with_fallback(path)
    except Exception as e:
        print(f"로드 실패: {e}")
        return

    required = {TIME_COL, LEAD_A_COL, BRAKE_COL}
    if not required.issubset(df.columns):
        print(f"필수 컬럼 없음: {required - set(df.columns)}")
        return

    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()
    brake_vals = pd.to_numeric(df[BRAKE_COL], errors="coerce").fillna(0).to_numpy()

    accel_vals = None
    if ACCEL_PEDAL_COL in df.columns:
        accel_vals = pd.to_numeric(df[ACCEL_PEDAL_COL], errors="coerce").fillna(0).to_numpy()

    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs, MERGE_GAP_SEC)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)

    # 최소 길이 필터 적용
    filtered_low = [s for s in low_segs if s.duration_sec >= MIN_DURATION_SEC]
    filtered_high = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    # 고감속 거리 컬럼
    gap_vals = pd.to_numeric(df[FRONT_GAP_COL], errors="coerce").to_numpy() if FRONT_GAP_COL in df.columns else None

    # 한 줄 요약 배열 출력(콘솔)
    print_summary_arrays_one_line(filtered_low, filtered_high, brake_vals, times_sec, accel_vals, gap_vals)

    # 새 CSV 생성
    out_df = build_low_segments_report_df(path.name, filtered_low, brake_vals, times_sec, accel_vals)
    out_path = path.with_name(f"{path.stem}_low_summary.csv")
    out_df.to_csv(out_path, index=False, encoding="utf-8-sig")
    print(f"[저장] {out_path}")

# 단일 파일을 읽어 저감속 요약 DataFrame만 반환(파일 저장 X)
def build_report_for_file(path: Path, also_print: bool = False) -> pd.DataFrame | None:
    try:
        df = load_csv_with_fallback(path)
    except Exception as e:
        print(f"[스킵: 로드 실패] {path.name} -> {e}")
        return None

    required = {TIME_COL, LEAD_A_COL, BRAKE_COL}
    if not required.issubset(df.columns):
        print(f"[스킵: 필수 컬럼 없음] {path.name} -> {required - set(df.columns)}")
        return None

    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()
    brake_vals = pd.to_numeric(df[BRAKE_COL], errors="coerce").fillna(0).to_numpy()

    accel_vals = None
    if ACCEL_PEDAL_COL in df.columns:
        accel_vals = pd.to_numeric(df[ACCEL_PEDAL_COL], errors="coerce").fillna(0).to_numpy()

    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs, MERGE_GAP_SEC)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)

    # 최소 길이 필터 적용
    filtered_low = [s for s in low_segs if s.duration_sec >= MIN_DURATION_SEC]
    filtered_high = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    # 요약 한 줄 프린트(옵션)
    if also_print:
        gap_vals = pd.to_numeric(df[FRONT_GAP_COL], errors="coerce").to_numpy() if FRONT_GAP_COL in df.columns else None
        print(f"\n[파일] {path.name}")
        print_summary_arrays_one_line(filtered_low, filtered_high, brake_vals, times_sec, accel_vals, gap_vals)

    # 보고서 DF 생성(참여자번호, 브레이크유형, 저감속구간의 길이, 브레이크세기, 반응시간)
    out_df = build_low_segments_report_df(path.name, filtered_low, brake_vals, times_sec, accel_vals)
    return out_df

# 단일 파일에서 고감속 요약 DF 반환
def build_high_report_for_file(path: Path) -> pd.DataFrame | None:
    try:
        df = load_csv_with_fallback(path)
    except Exception as e:
        print(f"[스킵: 로드 실패] {path.name} -> {e}")
        return None

    required = {TIME_COL, LEAD_A_COL, FRONT_GAP_COL}
    if not required.issubset(df.columns):
        print(f"[스킵: 필수 컬럼 없음] {path.name} -> {required - set(df.columns)}")
        return None

    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()

    # 세그먼트 추출 및 병합, 길이 필터
    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)
    filtered_high = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    gap_vals = pd.to_numeric(df[FRONT_GAP_COL], errors="coerce").to_numpy()
    return build_high_segments_report_df(path.name, filtered_high, gap_vals)

def sort_by_pid_and_brake(
    df: pd.DataFrame,
    pid_col: str,
    brake_col: str,
    seg_col: str | None = None
) -> pd.DataFrame:
    """
    정렬만 수행:
      1) 참여자번호 오름차순
      2) 브레이크유형 A,B,C,D 순
      3) (선택) 구간 번호 오름차순
    원본 참여자번호 값은 변경하지 않음.
    """
    if df.empty or pid_col not in df.columns or brake_col not in df.columns:
        return df
    df = df.copy()
    df["_pid_num"] = pd.to_numeric(df[pid_col], errors="coerce")
    order_map = {"A": 0, "B": 1, "C": 2, "D": 3}
    first_letter = df[brake_col].astype(str).str.extract(r"^([A-Z])", expand=False)
    df["_brake_order"] = first_letter.map(order_map).fillna(99).astype(int)
    sort_cols = ["_pid_num", "_brake_order"]
    if seg_col and seg_col in df.columns:
        df["_seg_num"] = pd.to_numeric(df[seg_col], errors="coerce")
        sort_cols.append("_seg_num")
    df = df.sort_values(sort_cols, kind="mergesort")
    drop_cols = ["_pid_num", "_brake_order"] + (["_seg_num"] if seg_col and seg_col in df.columns else [])
    return df.drop(columns=drop_cols)

def main():
    # 사용법:
    #  - 단일 파일 처리(콘솔 요약+개별 low_summary 저장): python UnityData2.py c:\path\to\file.csv
    #  - 폴더 전체 합쳐 저장(low/high): python UnityData2.py
    if len(sys.argv) > 1:
        arg = sys.argv[1]
        p = Path(arg)
        if not p.exists():
            p = ACCEL_DIR / arg
        if p.exists() and p.is_file():
            process_file(p)
        else:
            print(f"파일을 찾을 수 없습니다: {arg}")
        return

    # 원본 CSV만 선택(요약 파일 제외)
    csv_list = sorted([
        p for p in ACCEL_DIR.glob("*.csv")
        if "low_summary" not in p.name.lower() and "high_summary" not in p.name.lower()
    ])
    if not csv_list:
        print("CSV 파일을 찾을 수 없습니다.")
        return

    # 저감속 통합 파일
    all_low_rows: list[pd.DataFrame] = []
    for path in csv_list:
        df_out = build_report_for_file(path, also_print=False)
        if df_out is not None and not df_out.empty:
            all_low_rows.append(df_out)
    if all_low_rows:
        combined_low = pd.concat(all_low_rows, ignore_index=True)
        # 참여자번호 → 브레이크유형(A,B,C,D) → 저감속구간 번호 정렬(원번호 유지)
        combined_low = sort_by_pid_and_brake(combined_low, "참여자번호", "브레이크유형", "저감속구간 번호")
        out_path_low = ACCEL_DIR / "low_summary_all.csv"
        combined_low.to_csv(out_path_low, index=False, encoding="utf-8-sig")
        print(f"[저장] {out_path_low} (행 {len(combined_low)}개)")

    # 고감속 통합 파일(최소거리 = 이 계산에서의 선두차 거리 최소 배열)
    all_high_rows: list[pd.DataFrame] = []
    for path in csv_list:
        df_high = build_high_report_for_file(path)
        if df_high is not None and not df_high.empty:
            all_high_rows.append(df_high)
    if all_high_rows:
        combined_high = pd.concat(all_high_rows, ignore_index=True)
        # 참여자번호 → 브레이크유형(A,B,C,D) → 고감속구간 번호 정렬(원번호 유지)
        combined_high = sort_by_pid_and_brake(combined_high, "참여자번호", "브레이크유형", "고감속구간 번호")
        out_path_high = ACCEL_DIR / "high_summary_all.csv"
        combined_high.to_csv(out_path_high, index=False, encoding="utf-8-sig")
        print(f"[저장] {out_path_high} (행 {len(combined_high)}개)")
    else:
        print("고감속 요약에 포함할 데이터가 없습니다.")

if __name__ == "__main__":
    main()