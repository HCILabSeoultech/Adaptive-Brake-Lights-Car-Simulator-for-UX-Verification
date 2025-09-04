import pandas as pd
import numpy as np
from pathlib import Path

# 입력 / 출력 폴더
INPUT_DIR  = Path(r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled")
OUTPUT_DIR = Path(r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled_accel")
OUTPUT_DIR.mkdir(parents=True, exist_ok=True)

TIME_COL = "현재 시간"
LEAD_V_COL = "선두 차량 속도"
EGO_V_COL  = "실험 차량 속도"
LEAD_A_COL = "선두 차량 가속도"
EGO_A_COL  = "실험 차량 가속도"

KMH2MS = 1.0 / 3.6

def parse_time_to_seconds(t: str) -> float:
    h, m, s, ms = t.split(":")
    return int(h)*3600 + int(m)*60 + int(s) + int(ms)/1000.0

def compute_plateau_accel(times_sec: np.ndarray, speeds_kmh: np.ndarray) -> np.ndarray:
    """
    연속 동일 속도 구간(plateau)에 대해 다음 속도 변화 시점까지 Δv/Δt 로 단일 가속도 부여.
    마지막 plateau 는 0 유지.
    """
    n = len(speeds_kmh)
    acc = np.zeros(n, dtype=float)
    if n == 0:
        return acc
    seg_start = 0
    seg_speed = speeds_kmh[0]
    for i in range(1, n):
        if speeds_kmh[i] != seg_speed:
            t_start  = times_sec[seg_start]
            t_change = times_sec[i]
            dt = t_change - t_start
            if dt <= 1e-6:
                dt = 1e-3
            dv_kmh = speeds_kmh[i] - seg_speed
            a = (dv_kmh * KMH2MS) / dt
            acc[seg_start:i] = a
            seg_start = i
            seg_speed = speeds_kmh[i]
    return acc  # 마지막 plateau 는 0

def smooth_outlier_groups(acc: np.ndarray, threshold: float = 8.0) -> np.ndarray:
    """
    |acc| > threshold 구간을 선형 보간(or 엣지 확장)으로 완화.
    """
    acc = acc.copy()
    mask = np.abs(acc) > threshold
    if not mask.any():
        return acc
    n = len(acc)
    i = 0
    while i < n:
        if not mask[i]:
            i += 1
            continue
        g_start = i
        while i < n and mask[i]:
            i += 1
        g_end = i - 1
        prev_idx = g_start - 1
        next_idx = g_end + 1
        prev_val = acc[prev_idx] if prev_idx >= 0 else None
        next_val = acc[next_idx] if next_idx < n else None
        if prev_val is not None and next_val is not None:
            span = (g_end - g_start + 2)
            for k, idx in enumerate(range(g_start, g_end + 1), start=1):
                acc[idx] = prev_val + (next_val - prev_val) * (k / span)
        elif prev_val is not None:
            acc[g_start:g_end+1] = prev_val
        elif next_val is not None:
            acc[g_start:g_end+1] = next_val
        else:
            acc[g_start:g_end+1] = 0.0
    return acc

def refine_small_islands(acc: np.ndarray,
                          diff_threshold: float = 2.0,
                          island_max_len: int = 5,
                          neighbor_tolerance: float = 0.5) -> np.ndarray:
    """
    짧은 '섬' (앞뒤 plateau 값과 diff_threshold 이상 차이나고 길이 <= island_max_len,
    앞뒤 plateau 값이 서로 neighbor_tolerance 이내로 거의 같은 경우)을
    앞(=뒤) plateau 값으로 덮어써서 제거.
    예: -7 -7  -3 -3  -7 -7  -> -7 -7 -7 -7 -7 -7
    """
    a = acc.copy()
    n = len(a)
    if n < 3:
        return a

    i = 1
    while i < n - 1:
        prev_val = a[i - 1]
        # 섬 후보 시작 조건: 현재 값이 이전 plateau 와 충분히 다름
        if abs(a[i] - prev_val) >= diff_threshold:
            j = i
            # j 를 섬 끝 다음 지점으로 이동 (prev_val 과의 차이가 큰 동안)
            while j < n and abs(a[j] - prev_val) >= diff_threshold:
                j += 1
            # 섬 구간: [i, j-1]
            island_end = j - 1
            length = island_end - i + 1
            if j < n:  # 뒤쪽 anchor 존재
                next_val = a[j]
                # 앞뒤 plateau 거의 동일 & 섬 길이 제한 충족 & 섬 값들이 양쪽 plateau 와 모두 차이 큼
                if (length <= island_max_len and
                    abs(next_val - prev_val) < neighbor_tolerance):
                    # 섬 내부 전체를 prev_val (또는 (prev+next)/2) 로 덮어씀
                    fill_val = (prev_val + next_val) / 2.0  # 둘이 거의 같으므로 평균
                    a[i:j] = fill_val
                    i = j
                    continue
            else:
                # 끝까지가 섬이면 (뒤 anchor 없음) 통상 유지 -> 수정 안함
                pass
        i += 1
    return a

def collapse_return_noise(acc: np.ndarray,
                          anchor_tolerance: float = 0.4,
                          min_plateau_len: int = 2,
                          min_return_plateau_len: int = 2,
                          max_gap_len: int = 15,
                          diff_min: float = 1.0,
                          max_gap_range: float = 3.5,
                          min_sign_changes: int = 2) -> np.ndarray:
    """
    패턴: [anchor plateau] (gap 잡음) [anchor 로 거의 돌아온 plateau]
    조건을 모두 만족하면 gap 전체를 anchor 값으로 덮어씀.
    과도 평탄화를 막기 위해 여러 안전장치:
      - gap 길이 <= max_gap_len
      - gap 중앙/중위수 차이( anchor 와 ) >= diff_min -> 진짜로 벗어났다가 돌아온 것
      - gap 값 range <= max_gap_range -> 큰 추세 변화(새 plateau) 아니어야
      - gap 내부 값 변동(부호 전환) >= min_sign_changes -> 단순 추세 하나가 아님
      - 돌아온 plateau 길이 >= min_return_plateau_len
      - 앞/뒤 plateau 값이 anchor_tolerance 이내
    """
    a = acc.copy()
    n = len(a)
    if n < 5:
        return a

    def run_length(start: int, val: float) -> int:
        """anchor_tolerance 내에서 plateau 길이 측정"""
        j = start
        while j < n and abs(a[j] - val) < anchor_tolerance:
            j += 1
        return j - start

    i = 0
    while i < n:
        anchor_val = a[i]
        # 앞쪽 plateau 길이
        left_len = run_length(i, anchor_val)
        if left_len < min_plateau_len:
            i += 1
            continue
        gap_start = i + left_len
        if gap_start >= n - 1:
            break

        # gap 탐색: anchor 로 "돌아오는" 지점 찾기
        # j 는 potential return plateau 시작
        merged = False
        j = gap_start
        while j < n:
            # 잠재적 return 이 anchor 값 근처?
            if abs(a[j] - anchor_val) < anchor_tolerance:
                ret_len = run_length(j, anchor_val)
                if ret_len >= min_return_plateau_len:
                    gap_end = j  # gap 구간: [gap_start, j-1]
                    gap_len = gap_end - gap_start
                    if 0 < gap_len <= max_gap_len:
                        gap_vals = a[gap_start:gap_end]
                        if gap_vals.size:
                            median_diff = float(np.median(np.abs(gap_vals - anchor_val)))
                            rng = float(gap_vals.max() - gap_vals.min())
                            # sign change 계산
                            diffs = np.diff(gap_vals)
                            signs = np.sign(diffs)
                            # 0 제외 후 인접 부호 변화 수
                            nz = signs[signs != 0]
                            sign_changes = int(np.sum(nz[1:] * nz[:-1] < 0))
                            if (median_diff >= diff_min and
                                rng <= max_gap_range and
                                sign_changes >= min_sign_changes):
                                # 병합 수행
                                a[gap_start:gap_end] = anchor_val
                                merged = True
                                i = j + ret_len  # return plateau 뒤로 이동
                                break
                    # 조건 불충족 -> 새 anchor 로 간주
                    break
                # return plateau 길이 부족 -> 계속 확장
            # gap 길이 초과시 중단
            if j - gap_start > max_gap_len:
                break
            j += 1

        if not merged:
            # 병합 실패 -> 다음 위치로 진행 (새 anchor 탐색)
            i += max(1, left_len)

    return a

def recompute_and_smooth(df: pd.DataFrame,
                         outlier_threshold: float = 8.0) -> pd.DataFrame:
    t_sec = df[TIME_COL].apply(parse_time_to_seconds).to_numpy()
    lead_v = df[LEAD_V_COL].astype(float).to_numpy()
    ego_v  = df[EGO_V_COL].astype(float).to_numpy()
    lead_a_raw = compute_plateau_accel(t_sec, lead_v)
    ego_a_raw  = compute_plateau_accel(t_sec, ego_v)

    lead_a = smooth_outlier_groups(lead_a_raw, threshold=outlier_threshold)
    ego_a  = smooth_outlier_groups(ego_a_raw,  threshold=outlier_threshold)

    lead_a = refine_small_islands(lead_a, diff_threshold=2.0,
                                  island_max_len=5, neighbor_tolerance=0.5)
    ego_a  = refine_small_islands(ego_a, diff_threshold=2.0,
                                  island_max_len=5, neighbor_tolerance=0.5)

    # 복귀형 잡음(여러 값 흔들림 후 원래 plateau 로 짧게 복귀) 병합 (안전 조건 포함)
    lead_a = collapse_return_noise(lead_a,
                                   anchor_tolerance=0.4,
                                   min_plateau_len=2,
                                   min_return_plateau_len=2,
                                   max_gap_len=12,
                                   diff_min=1.2,
                                   max_gap_range=3.0,
                                   min_sign_changes=2)
    ego_a  = collapse_return_noise(ego_a,
                                   anchor_tolerance=0.4,
                                   min_plateau_len=2,
                                   min_return_plateau_len=2,
                                   max_gap_len=12,
                                   diff_min=1.2,
                                   max_gap_range=3.0,
                                   min_sign_changes=2)

    df[LEAD_A_COL] = lead_a
    df[EGO_A_COL]  = ego_a
    return df

def process_one(csv_path: Path, outlier_threshold: float = 8.0) -> tuple[bool, str]:
    try:
        df = pd.read_csv(csv_path, encoding="utf-8-sig")
    except UnicodeDecodeError:
        df = pd.read_csv(csv_path, encoding="cp949")
    required = [TIME_COL, LEAD_V_COL, EGO_V_COL, LEAD_A_COL, EGO_A_COL]
    if any(c not in df.columns for c in required):
        return False, f"skip(missing cols): {csv_path.name}"
    df = recompute_and_smooth(df, outlier_threshold=outlier_threshold)
    out_path = OUTPUT_DIR / csv_path.name  # 동일 이름으로 출력 폴더에 저장
    df.to_csv(out_path, index=False, encoding="utf-8-sig")
    return True, f"ok: {csv_path.name}"

def main():
    csv_files = sorted(INPUT_DIR.glob("*.csv"))
    success = 0
    logs = []
    for fp in csv_files:
        ok, msg = process_one(fp)
        if ok:
            success += 1
        logs.append(msg)
    print("\n".join(logs))
    print(f"\n완료: {success}/{len(csv_files)} 파일 처리 (출력 폴더: {OUTPUT_DIR})")

if __name__ == "__main__":
    main()