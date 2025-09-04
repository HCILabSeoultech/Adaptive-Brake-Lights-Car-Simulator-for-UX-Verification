from __future__ import annotations
from dataclasses import dataclass
from pathlib import Path
from typing import List, Tuple
import sys
import pandas as pd
import numpy as np

# 데이터 폴더
ACCEL_DIR = Path(
    r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel"
)

# 컬럼명
TIME_COL = "현재 시간"
LEAD_A_COL = "선두 차량 가속도"
BRAKE_COL  = "브레이크 세기"
ACCEL_PEDAL_COL = "엑셀 세기"
FRONT_GAP_COL = "실시간 앞차 간격"
COLLISION_COL = "충돌여부"

# 저감속/고감속 기준 (UnityData2.py 동일)
LOW_DECEL_MIN = -4.0
LOW_DECEL_MAX = -1.5
HIGH_DECEL_MAX = -5.0

# 인접 구간 병합 기준(초)
MERGE_GAP_SEC = 1.0
# 최소 구간 길이(초): 이 값 미만은 구간으로 판단하지 않음
MIN_DURATION_SEC = 0.8

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

TARGET_SUFFIXES = ("Unity.csv", "Unity_cleaned.csv")
def is_target_file(name: str) -> bool:
    return any(name.endswith(suf) for suf in TARGET_SUFFIXES)

def parse_time_to_seconds(t: str) -> float:
    # "HH:MM:SS:ms" -> 초
    try:
        h, m, s, ms = t.split(":")
        return int(h) * 3600 + int(m) * 60 + int(s) + int(ms) / 1000.0
    except Exception:
        return float("nan")

def parse_meta_from_filename(filename: str) -> Tuple[str, str]:
    """
    <실험번호>_<참여자번호>_<참여자이름>_<브레이크유형>_<성별>_<운전경력>[_Unity].csv
    반환: (참여자번호, 브레이크유형), 실패 시 ("","")
    """
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

def print_one_csv_time_ordered(path: Path) -> None:
    pid, brake_type = parse_meta_from_filename(path.name)

    # 대상 파일만 처리
    if not is_target_file(path.name):
        print(f"[스킵: 대상 아님] {path.name} (허용: {', '.join(TARGET_SUFFIXES)})")
        return

    try:
        df = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df = pd.read_csv(path, encoding="cp949")

    # 필수 컬럼 체크
    required = {TIME_COL, LEAD_A_COL, BRAKE_COL}
    for col in required:
        if col not in df.columns:
            print(f"[스킵: 필수 컬럼 없음] {path.name} -> {required - set(df.columns)}")
            return

    # 값 준비
    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].astype(str).apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()
    brake_vals = pd.to_numeric(df[BRAKE_COL], errors="coerce").fillna(0).to_numpy()

    accel_vals = None
    if ACCEL_PEDAL_COL in df.columns:
        accel_vals = pd.to_numeric(df[ACCEL_PEDAL_COL], errors="coerce").fillna(0).to_numpy()

    # 실시간 앞차 간격 (옵션)
    gap_vals = None
    if FRONT_GAP_COL in df.columns:
        gap_vals = pd.to_numeric(df[FRONT_GAP_COL], errors="coerce").to_numpy()

    # 구간 추출/병합/필터
    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs, MERGE_GAP_SEC)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)
    low_segs = [s for s in low_segs if s.duration_sec >= MIN_DURATION_SEC]
    high_segs = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    # 시간 순서로 합치기
    combined = [(s.start_sec, "저감속", s) for s in low_segs] + [(s.start_sec, "고감속", s) for s in high_segs]
    combined.sort(key=lambda x: x[0])

    # 감속정도별 구간번호 카운터
    counters = {"저감속": 0, "고감속": 0}

    print(f"\n[파일] {path.name}")
    print("[참여자번호,브레이크유형,감속정도,구간번호,브레이크세기,반응시간,인지시간,브레이크횟수,실시간앞차간격최소,실시간앞차간격최대]")

    for _, decel_kind, s in combined:
        counters[decel_kind] += 1
        seg_no = counters[decel_kind]

        # 평균 브레이크 세기
        if s.end_idx >= s.start_idx:
            seg_slice = brake_vals[s.start_idx:s.end_idx+1]
            avg_brake = float(np.nanmean(seg_slice)) if len(seg_slice) else 0.0
        else:
            avg_brake = 0.0

        # 반응시간: 브레이크 > 0 시점, 이미 >0 상태면 0 (없으면 0)
        reaction_time = 0.0
        if s.end_idx >= s.start_idx:
            if brake_vals[s.start_idx] > 0:
                reaction_time = 0.0
            else:
                for i in range(s.start_idx, s.end_idx + 1):
                    if brake_vals[i] > 0:
                        reaction_time = float(times_sec[i] - s.start_sec)
                        break

        # 인지시간: 엑셀이 0보다 큰 상태로 시작했다가 0.2 강하한 시점 (아닐 경우 0)
        perception_time = 0.0
        if accel_vals is not None and s.end_idx >= s.start_idx:
            start_val = float(accel_vals[s.start_idx])
            if start_val > 0.0:
                for i in range(s.start_idx, s.end_idx + 1):
                    if start_val - float(accel_vals[i]) >= 0.2:
                        perception_time = float(times_sec[i] - s.start_sec)
                        break

        # 구간 내 실시간 앞차 간격 최소/최대
        gap_min = 0.0
        gap_max = 0.0
        if gap_vals is not None and s.end_idx >= s.start_idx:
            seg_gap = gap_vals[s.start_idx:s.end_idx+1]
            valid = seg_gap[~np.isnan(seg_gap)]
            if len(valid) > 0:
                gap_min = float(np.min(valid))
                gap_max = float(np.max(valid))

        # 브레이크 밟은 횟수
        # - 구간 내부에서 0 -> (>0)로 시작한 사이클만 집계
        # - 구간 끝까지 0으로 내려오지 않아도 1회로 집계
        brake_press_count = 0
        if s.end_idx >= s.start_idx:
            # 구간 시작 시 이미 밟고 있으면(>0) 구간 외부에서 시작한 것으로 간주 -> 미집계 상태로 시작
            pressing = bool(brake_vals[s.start_idx] > 0)
            started_inside = False  # 해당 press가 구간 내부에서 시작했는지
            # j는 start+1부터 순회하며 이전 값과의 전이 관찰
            for j in range(s.start_idx + 1, s.end_idx + 1):
                prev = float(brake_vals[j - 1])
                cur = float(brake_vals[j])
                if not pressing:
                    # 0 -> (>0): 구간 내부 시작
                    if prev <= 0 and cur > 0:
                        pressing = True
                        started_inside = True
                else:
                    # (>0) -> 0: 사이클 종료
                    if prev > 0 and cur <= 0:
                        if started_inside:
                            brake_press_count += 1
                        pressing = False
                        started_inside = False
            # 구간 종료 시점까지 0으로 내려오지 않은 내부 시작 press도 1회로 인정
            if pressing and started_inside:
                brake_press_count += 1

        # 프린트
        pid_out = pid
        brake_type_out = brake_type
        avg_brake_out = round(avg_brake, 5)
        reaction_out = round(float(reaction_time), 3)
        perception_out = round(float(perception_time), 3)
        gap_min_out = round(float(gap_min), 5)
        gap_max_out = round(float(gap_max), 5)

        print(f"{pid_out},{brake_type_out},{decel_kind},{seg_no},{avg_brake_out},{reaction_out},{perception_out},{brake_press_count},{gap_min_out},{gap_max_out}")

def collect_rows_from_csv(path: Path) -> List[dict]:
    rows: List[dict] = []
    pid, brake_type = parse_meta_from_filename(path.name)

    if not is_target_file(path.name):
        return rows

    try:
        df = pd.read_csv(path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df = pd.read_csv(path, encoding="cp949")

    required = {TIME_COL, LEAD_A_COL, BRAKE_COL}
    for col in required:
        if col not in df.columns:
            return rows

    values = pd.to_numeric(df[LEAD_A_COL], errors="coerce").to_numpy()
    times_sec = df[TIME_COL].astype(str).apply(parse_time_to_seconds).to_numpy()
    time_strs = df[TIME_COL].astype(str).tolist()
    brake_vals = pd.to_numeric(df[BRAKE_COL], errors="coerce").fillna(0).to_numpy()

    accel_vals = None
    if ACCEL_PEDAL_COL in df.columns:
        accel_vals = pd.to_numeric(df[ACCEL_PEDAL_COL], errors="coerce").fillna(0).to_numpy()

    gap_vals = None
    if FRONT_GAP_COL in df.columns:
        gap_vals = pd.to_numeric(df[FRONT_GAP_COL], errors="coerce").to_numpy()

    low_segs, high_segs = extract_segments(values, times_sec, time_strs, path.name)
    low_segs = merge_segments_by_time_gap(low_segs, MERGE_GAP_SEC)
    high_segs = merge_segments_by_time_gap(high_segs, MERGE_GAP_SEC)
    low_segs = [s for s in low_segs if s.duration_sec >= MIN_DURATION_SEC]
    high_segs = [s for s in high_segs if s.duration_sec >= MIN_DURATION_SEC]

    combined = [(s.start_sec, "저감속", s) for s in low_segs] + [(s.start_sec, "고감속", s) for s in high_segs]
    combined.sort(key=lambda x: x[0])

    counters = {"저감속": 0, "고감속": 0}

    for _, decel_kind, s in combined:
        counters[decel_kind] += 1
        seg_no = counters[decel_kind]

        # 평균 브레이크 세기
        if s.end_idx >= s.start_idx:
            seg_slice = brake_vals[s.start_idx:s.end_idx+1]
            avg_brake = float(np.nanmean(seg_slice)) if len(seg_slice) else 0.0
        else:
            avg_brake = 0.0

        # 반응시간
        reaction_time = 0.0
        if s.end_idx >= s.start_idx:
            if brake_vals[s.start_idx] > 0:
                reaction_time = 0.0
            else:
                for i in range(s.start_idx, s.end_idx + 1):
                    if brake_vals[i] > 0:
                        reaction_time = float(times_sec[i] - s.start_sec)
                        break

        # 인지시간
        perception_time = 0.0
        if accel_vals is not None and s.end_idx >= s.start_idx:
            start_val = float(accel_vals[s.start_idx])
            if start_val > 0.0:
                for i in range(s.start_idx, s.end_idx + 1):
                    if start_val - float(accel_vals[i]) >= 0.2:
                        perception_time = float(times_sec[i] - s.start_sec)
                        break

        # 앞차 간격 최소/최대
        gap_min = 0.0
        gap_max = 0.0
        if gap_vals is not None and s.end_idx >= s.start_idx:
            seg_gap = gap_vals[s.start_idx:s.end_idx+1]
            valid = seg_gap[~np.isnan(seg_gap)]
            if len(valid) > 0:
                gap_min = float(np.min(valid))
                gap_max = float(np.max(valid))

        # 브레이크 횟수 (구간 내 0->양수 시작, 끝까지 0 미복귀도 1회 인정)
        brake_press_count = 0
        if s.end_idx >= s.start_idx:
            pressing = bool(brake_vals[s.start_idx] > 0)
            started_inside = False
            for j in range(s.start_idx + 1, s.end_idx + 1):
                prev = float(brake_vals[j - 1])
                cur = float(brake_vals[j])
                if not pressing:
                    if prev <= 0 and cur > 0:
                        pressing = True
                        started_inside = True
                else:
                    if prev > 0 and cur <= 0:
                        if started_inside:
                            brake_press_count += 1
                        pressing = False
                        started_inside = False
            if pressing and started_inside:
                brake_press_count += 1

        rows.append({
            "참여자번호": pid,
            "브레이크유형": brake_type,
            "감속정도": decel_kind,
            "구간번호": int(seg_no),
            "브레이크세기": round(float(avg_brake), 5),
            "반응시간": round(float(reaction_time), 3),
            "인지시간": round(float(perception_time), 3),
            "브레이크횟수": int(brake_press_count),
            "실시간앞차간격최소": round(float(gap_min), 5),
            "실시간앞차간격최대": round(float(gap_max), 5),
        })

    return rows

def main():
    # 폴더 내 대상 CSV 전체 처리하여 하나의 CSV로 저장
    if len(sys.argv) > 1:
        # 인자로 파일 하나를 주면 기존 프린트 동작 유지
        p = Path(sys.argv[1])
        if not is_target_file(p.name):
            print(f"{', '.join(TARGET_SUFFIXES)} 파일만 처리합니다: {p.name}")
            return
        print_one_csv_time_ordered(p)
        return

    cands = [pp for pp in sorted(ACCEL_DIR.glob("*.csv")) if is_target_file(pp.name)]
    if not cands:
        print(f"대상 파일을 찾을 수 없습니다. 허용: {', '.join(TARGET_SUFFIXES)}")
        return

    all_rows: List[dict] = []
    file_count = 0
    for p in cands:
        rows = collect_rows_from_csv(p)
        if rows:
            all_rows.extend(rows)
            file_count += 1

    if not all_rows:
        print("처리할 데이터가 없습니다.")
        return

    df = pd.DataFrame(all_rows)
    # 참여자번호 숫자 정렬 보조 컬럼
    df["_참여자번호_num"] = pd.to_numeric(df["참여자번호"], errors="coerce")
    df.sort_values(by=["_참여자번호_num", "브레이크유형"], inplace=True, kind="mergesort")
    df.drop(columns=["_참여자번호_num"], inplace=True)

    out_path = ACCEL_DIR / "Unity_all_results.csv"
    df.to_csv(out_path, index=False, encoding="utf-8-sig")
    print(f"총 {file_count}개 파일, {len(df)}개 구간 결과를 저장했습니다: {out_path}")

if __name__ == "__main__":
    main()