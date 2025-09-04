from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
from typing import List, Tuple
import sys
import numpy as np
import pandas as pd

# 데이터 폴더 (기존과 동일 경로)
ACCEL_DIR = Path(
    r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel"
)

# 컬럼명
TIME_COL = "현재 시간"
LEAD_A_COL = "선두 차량 가속도"
BRAKE_COL  = "브레이크 세기"
ACCEL_PEDAL_COL = "엑셀 세기"           # 사용 안 하지만 호환 유지
FRONT_GAP_COL = "실시간 앞차 간격"      # 사용 안 하지만 호환 유지
COLLISION_COL = "충돌여부"

# 저감속/고감속 기준
LOW_DECEL_MIN = -4.0
LOW_DECEL_MAX = -1.5
HIGH_DECEL_MAX = -5.0

# 구간 병합/필터 기준
MERGE_GAP_SEC = 1.0
MIN_DURATION_SEC = 0.8

TARGET_SUFFIXES = ("Unity.csv", "Unity_cleaned.csv")
def is_target_file(name: str) -> bool:
    return any(name.endswith(suf) for suf in TARGET_SUFFIXES)

@dataclass
class Segment:
    seg_type: str   # "low" | "high"
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
    try:
        h, m, s, ms = t.split(":")
        return int(h) * 3600 + int(m) * 60 + int(s) + int(ms) / 1000.0
    except Exception:
        return float("nan")

def parse_meta_from_filename(filename: str) -> Tuple[str, str]:
    # <실험번호>_<참여자번호>_<참여자이름>_<브레이크유형>_<성별>_<운전경력>[_Unity].csv
    stem = Path(filename).stem
    parts = stem.split("_")
    if len(parts) >= 4:
        return parts[1], parts[3]
    return "", ""

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
            min_val=float(np.nanmin(sub_vals)) if len(sub_vals) else float("nan"),
            max_val=float(np.nanmax(sub_vals)) if len(sub_vals) else float("nan"),
            mean_val=float(np.nanmean(sub_vals)) if len(sub_vals) else float("nan"),
            start_sec=float(times_sec[start]),
            end_sec=float(times_sec[end]),
        )
        (low_segments if seg_type == "low" else high_segments).append(seg)

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

def compute_press_durations_in_segment(brake_vals: np.ndarray, times_sec: np.ndarray, s: Segment) -> List[float]:
    durations: List[float] = []
    if s.end_idx < s.start_idx:
        return durations

    pressing = bool(brake_vals[s.start_idx] > 0)  # 구간 시작 전에 밟던 상태는 배제
    started_inside = False
    press_start_t = None

    for j in range(s.start_idx + 1, s.end_idx + 1):
        prev = float(brake_vals[j - 1])
        cur = float(brake_vals[j])

        if not pressing:
            if prev <= 0 and cur > 0:
                pressing = True
                started_inside = True
                press_start_t = float(times_sec[j])
        else:
            if prev > 0 and cur <= 0:
                if started_inside and press_start_t is not None:
                    durations.append(float(times_sec[j]) - press_start_t)
                pressing = False
                started_inside = False
                press_start_t = None

    # 구간 끝까지 0으로 내려오지 않은 경우, 구간 끝까지로 지속시간 계산
    if pressing and started_inside and press_start_t is not None:
        durations.append(float(times_sec[s.end_idx]) - press_start_t)

    return durations

def summarize_one_file(path: Path) -> List[dict]:
    rows: List[dict] = []
    if not is_target_file(path.name):
        return rows

    pid, brake_type = parse_meta_from_filename(path.name)

    try:
        df = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df = pd.read_csv(path, encoding="cp949")

    required = {TIME_COL, LEAD_A_COL, BRAKE_COL}
    for col in required:
        if col not in df.columns:
            # 필수 컬럼 없으면 건너뜀
            return rows

    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].astype(str).apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()
    brake_vals = pd.to_numeric(df[BRAKE_COL], errors="coerce").fillna(0).to_numpy()

    # 구간 추출/병합/필터
    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs, MERGE_GAP_SEC)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)
    low_segs = [s for s in low_segs if s.duration_sec >= MIN_DURATION_SEC]
    high_segs = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    def build_row_for_group(decel_kind: str, segs: List[Segment]) -> dict:
        # 브레이크 세기 표준편차(해당 감속 구간의 모든 샘플)
        all_brake_samples: List[float] = []
        # 브레이크 지속시간(press) 리스트
        all_press_durations: List[float] = []
        # 충돌 횟수(구간 단위)
        collision_count = 0

        # 충돌여부 컬럼 존재 시만 사용
        has_collision = COLLISION_COL in df.columns
        allowed = {"normal", "changeline"}

        for s in segs:
            # 세기 표본
            if s.end_idx >= s.start_idx:
                all_brake_samples.extend([float(x) for x in brake_vals[s.start_idx:s.end_idx+1]])

            # press 지속시간
            all_press_durations.extend(compute_press_durations_in_segment(brake_vals, times_sec, s))

            # 충돌 여부
            if has_collision:
                seg_vals = (
                    df[COLLISION_COL]
                    .iloc[s.start_idx:s.end_idx+1]
                    .astype(str)
                    .str.strip()
                    .str.lower()
                    .tolist()
                )
                # allowed 외 값이 하나라도 있으면 그 구간은 충돌 1회
                collided = any(v not in allowed and v not in ("", "nan") for v in seg_vals)
                if collided:
                    collision_count += 1

        brake_std = float(np.std(all_brake_samples)) if len(all_brake_samples) > 0 else 0.0
        press_std = float(np.std(all_press_durations)) if len(all_press_durations) > 0 else 0.0

        return {
            "참여자번호": pid,
            "브레이크유형": brake_type,
            "감속정도": decel_kind,
            "브레이크지속시간표준편차": round(press_std, 5),
            "브레이크세기표준편차": round(brake_std, 5),
            "충돌횟수": int(collision_count),
        }

    # 저감속/고감속 각각 한 행 생성(구간이 없으면 0으로 채움)
    rows.append(build_row_for_group("저감속", low_segs))
    rows.append(build_row_for_group("고감속", high_segs))

    return rows

def main():
    # 인자로 파일 하나 주면 그 파일만 요약
    if len(sys.argv) > 1:
        p = Path(sys.argv[1])
        if not is_target_file(p.name):
            print(f"대상 아님: {p.name} (허용: {', '.join(TARGET_SUFFIXES)})")
            return
        rows = summarize_one_file(p)
        if not rows:
            print("데이터 없음")
            return
        df = pd.DataFrame(rows)
        # 정렬: 참여자번호(숫자), 브레이크유형, 감속정도(저감속->고감속)
        order = {"저감속": 0, "고감속": 1}
        df["_p"] = pd.to_numeric(df["참여자번호"], errors="coerce")
        df["_o"] = df["감속정도"].map(order)
        df.sort_values(by=["_p", "브레이크유형", "_o"], inplace=True, kind="mergesort")
        df.drop(columns=["_p", "_o"], inplace=True)
        print(df.to_csv(index=False, encoding="utf-8-sig"))
        return

    # 폴더 내 전체 요약
    cands = [pp for pp in sorted(ACCEL_DIR.glob("*.csv")) if is_target_file(pp.name)]
    if not cands:
        print(f"대상 파일을 찾을 수 없습니다. 허용: {', '.join(TARGET_SUFFIXES)}")
        return

    all_rows: List[dict] = []
    for p in cands:
        all_rows.extend(summarize_one_file(p))

    if not all_rows:
        print("처리할 데이터가 없습니다.")
        return

    df = pd.DataFrame(all_rows)
    order = {"저감속": 0, "고감속": 1}
    df["_p"] = pd.to_numeric(df["참여자번호"], errors="coerce")
    df["_o"] = df["감속정도"].map(order)
    df.sort_values(by=["_p", "브레이크유형", "_o"], inplace=True, kind="mergesort")
    df.drop(columns=["_p", "_o"], inplace=True)

    out_path = ACCEL_DIR / "Unity_brake_std_summary.csv"
    df.to_csv(out_path, index=False, encoding="utf-8-sig")
    print(f"{len(df)}행 저장: {out_path}")

if __name__ == "__main__":
    main()