import pandas as pd
from typing import List, Tuple, Union
import statistics  # 추가
from pathlib import Path

def _parse_time_to_seconds(t: Union[str, float, int]) -> float:
    t = str(t)
    parts = t.split(':')
    if len(parts) == 4:
        h, m, s, ms = parts
        try:
            return int(h)*3600 + int(m)*60 + int(s) + int(ms)/1000.0
        except ValueError:
            pass
    elif len(parts) == 3:
        h, m, s = parts
        try:
            return int(h)*3600 + int(m)*60 + float(s)
        except ValueError:
            pass
    return 0.0

def _replace_leading_constant_with_zero(series: pd.Series,
                                        constant_value: float = 0.5,
                                        tolerance: float = 1e-9,
                                        max_frames: Union[int, None] = None) -> pd.Series:
    """
    맨 앞(선두)에서 constant_value(±tolerance)인 연속 구간만 0으로 치환.
    첫 번째로 다른 값이 나오면 즉시 중단. (중간 이후 0.5는 그대로 유지)
    max_frames 지정 시 그 수만큼까지만 치환.
    """
    s = series.copy()
    changed = 0
    for i, v in enumerate(series):
        if abs(v - constant_value) <= tolerance:
            s.iat[i] = 0.0
            changed += 1
            if max_frames is not None and changed >= max_frames:
                break
        else:
            break
    return s

def count_pedal_presses(df,
                        pedal_col='브레이크 세기',
                        time_col='현재 시간',
                        zero_threshold=1e-6,
                        require_release=True,
                        return_times=False,
                        return_durations=False,
                        return_max_values=False,
                        duration_precision: int = 2,
                        max_precision: int = 2,
                        # 새 옵션: 초반 고정값(예: 0.5)만 0으로 처리
                        treat_initial_constant_as_zero: bool = True,
                        initial_constant_value: float = 0.5,
                        initial_constant_tolerance: float = 1e-6,
                        initial_constant_max_frames: Union[int, None] = None
                        ) -> Union[int, Tuple]:
    """
    0 -> (>0 … >0) -> 0 을 1회로 카운트.
    treat_initial_constant_as_zero=True 이면 '파일 맨 앞'의 연속된 initial_constant_value 만 0으로 간주.
    (중간에 다시 등장하는 동일값은 변경하지 않음)
    """
    if pedal_col not in df.columns:
        raise ValueError(f"열 '{pedal_col}' 없음. 실제 열들: {list(df.columns)}")
    if time_col not in df.columns:
        raise ValueError(f"열 '{time_col}' 없음. 실제 열들: {list(df.columns)}")

    series = df[pedal_col].fillna(0)

    if treat_initial_constant_as_zero:
        series = _replace_leading_constant_with_zero(
            series,
            constant_value=initial_constant_value,
            tolerance=initial_constant_tolerance,
            max_frames=initial_constant_max_frames
        )

    start_times: List[str] = []
    durations: List[float] = []
    max_values: List[float] = []

    pressing = False
    start_time_value = None
    current_max = None
    count = 0

    for idx, val in series.items():
        is_zero = abs(val) <= zero_threshold

        if not pressing:
            if not is_zero:
                pressing = True
                start_time_value = df.at[idx, time_col]
                current_max = float(val)
                if return_times:
                    start_times.append(start_time_value)
        else:
            if not is_zero:
                if current_max is None or val > current_max:
                    current_max = float(val)
            else:
                count += 1
                if return_durations and start_time_value is not None:
                    release_time_value = df.at[idx, time_col]
                    start_sec = _parse_time_to_seconds(start_time_value)
                    release_sec = _parse_time_to_seconds(release_time_value)
                    durations.append(max(0.0, release_sec - start_sec))
                if return_max_values and current_max is not None:
                    max_values.append(current_max)
                pressing = False
                start_time_value = None
                current_max = None

    # 끝에서 아직 밟고 있음
    if pressing:
        if not require_release:
            count += 1
            if return_durations and start_time_value is not None:
                end_time_value = df.iloc[-1][time_col]
                start_sec = _parse_time_to_seconds(start_time_value)
                end_sec = _parse_time_to_seconds(end_time_value)
                durations.append(max(0.0, end_sec - start_sec))
            if return_max_values and current_max is not None:
                max_values.append(current_max)
        else:
            # 미완성 시작 제거
            if return_times and start_times:
                start_times.pop()

    if return_durations:
        durations = [round(d, duration_precision) for d in durations]
    if return_max_values:
        max_values = [round(m, max_precision) for m in max_values]

    outputs = [count]
    if return_times:
        outputs.append(start_times)
    if return_durations:
        outputs.append(durations)
    if return_max_values:
        outputs.append(max_values)

    return outputs[0] if len(outputs) == 1 else tuple(outputs)

# alias
count_brake_presses = count_pedal_presses

def get_pedal_press_lists(file_path: Union[str, Path],
                          pedal_col: str,
                          time_col: str = '현재 시간',
                          zero_threshold: float = 1e-6,
                          require_release: bool = True,
                          duration_precision: int = 2,
                          max_precision: int = 2,
                          treat_initial_constant_as_zero: bool = True,
                          initial_constant_value: float = 0.5,
                          initial_constant_tolerance: float = 1e-6,
                          initial_constant_max_frames: Union[int, None] = 200
                          ) -> Tuple[List[float], List[float]]:
    """
    단일 CSV 파일에서 지정 페달 컬럼(pedal_col)에 대한
    (durations_list, max_values_list) 반환.
    durations_list: 각 press 의 지속시간 (초)
    max_values_list: 각 press 구간 내 최대 페달 값
    """
    file_path = Path(file_path)
    try:
        df = pd.read_csv(file_path, encoding='utf-8-sig')
    except UnicodeDecodeError:
        df = pd.read_csv(file_path, encoding='cp949')

    common_kwargs = dict(
        time_col=time_col,
        zero_threshold=zero_threshold,
        require_release=require_release,
        return_times=True,
        return_durations=True,
        return_max_values=True,
        duration_precision=duration_precision,
        max_precision=max_precision,
        treat_initial_constant_as_zero=treat_initial_constant_as_zero,
        initial_constant_value=initial_constant_value,
        initial_constant_tolerance=initial_constant_tolerance,
        initial_constant_max_frames=initial_constant_max_frames
    )

    result = count_pedal_presses(df, pedal_col=pedal_col, **common_kwargs)
    # 반환 구조: (count, starts, durations, max_values)
    _, _, durations, max_values = result
    return durations, max_values


def get_brake_press_lists(file_path: Union[str, Path], **kwargs) -> Tuple[List[float], List[float]]:
    """
    브레이크 세기 컬럼('브레이크 세기')에 대한 (durations, max_values) 반환.
    kwargs 는 get_pedal_press_lists 와 동일.
    """
    return get_pedal_press_lists(file_path, pedal_col='브레이크 세기', **kwargs)


def get_accel_press_lists(file_path: Union[str, Path], **kwargs) -> Tuple[List[float], List[float]]:
    """
    엑셀 세기 컬럼('엑셀 세기')에 대한 (durations, max_values) 반환.
    kwargs 는 get_pedal_press_lists 와 동일.
    """
    return get_pedal_press_lists(file_path, pedal_col='엑셀 세기', **kwargs)

def _get_brake_pedal_stats(file_path: Union[str, Path], **kwargs):
    b_durs, b_maxs = get_brake_press_lists(file_path)
    if not b_durs:
        return {"count": 0, "min_dur": 0.0, "max_dur": 0.0, "avg_dur": 0.0, "std_dur": 0.0, "min_max": 0.0, "max_max": 0.0, "avg_max": 0.0, "std_max": 0.0}
    mn_durs = min(b_durs)
    mx_durs = max(b_durs)
    avg_durs = statistics.mean(b_durs)
    std_durs = statistics.stdev(b_durs) if len(b_durs) > 1 else 0.0

    mn_maxs = min(b_maxs) if b_maxs else 0.0
    mx_maxs = max(b_maxs) if b_maxs else 0.0
    avg_maxs = statistics.mean(b_maxs) if b_maxs else 0.0
    std_maxs = statistics.stdev(b_maxs) if len(b_maxs) > 1 else 0.0

    return {"count": len(b_durs), "min_dur": mn_durs, "max_dur": mx_durs, "avg_dur": avg_durs, "std_dur": std_durs,
            "min_max": mn_maxs, "max_max": mx_maxs, "avg_max": avg_maxs, "std_max": std_maxs}

def _get_accel_pedal_stats(file_path: Union[str, Path], **kwargs):
    a_durs, a_maxs = get_accel_press_lists(file_path)
    if not a_durs:
        return {"count": 0, "min_dur": 0.0, "max_dur": 0.0, "avg_dur": 0.0, "std_dur": 0.0, "min_max": 0.0, "max_max": 0.0, "avg_max": 0.0, "std_max": 0.0}

    mn_durs = min(a_durs)
    mx_durs = max(a_durs)
    avg_durs = statistics.mean(a_durs)
    std_durs = statistics.stdev(a_durs) if len(a_durs) > 1 else 0.0

    mn_maxs = min(a_maxs) if a_maxs else 0.0
    mx_maxs = max(a_maxs) if a_maxs else 0.0
    avg_maxs = statistics.mean(a_maxs) if a_maxs else 0.0
    std_maxs = statistics.stdev(a_maxs) if len(a_maxs) > 1 else 0.0

    return {"count": len(a_durs), "min_dur": mn_durs, "max_dur": mx_durs, "avg_dur": avg_durs, "std_dur": std_durs,
            "min_max": mn_maxs, "max_max": mx_maxs, "avg_max": avg_maxs, "std_max": std_maxs}

# 사용 예 (외부에서):
# b_durs, b_max = get_brake_press_lists(csv_path)
# a_durs, a_max = get_accel_press_lists(csv_path)

# __main__ 테스트 예시 (기존 코드 유지 가능)
if __name__ == "__main__":
    file_path = r"C:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing\realtime_gsr_ppg_facial_recognizer_including_data_processing\Unity_divided_cleaned_labeled\P40_10_이수영_D면적변화제동등_남자_OneToTwo_Unity.csv"
    b_durs, b_max = get_brake_press_lists(file_path)
    a_durs, a_max = get_accel_press_lists(file_path)
    print("브레이크 (durations, max):", b_durs, b_max)
    print("엑셀    (durations, max):", a_durs, a_max)

    try:
        df = pd.read_csv(file_path, encoding='utf-8-sig')
    except UnicodeDecodeError:
        df = pd.read_csv(file_path, encoding='cp949')

    common_kwargs = dict(
        time_col='현재 시간',
        zero_threshold=1e-6,
        require_release=True,
        return_times=True,
        return_durations=True,
        return_max_values=True,
        duration_precision=2,
        max_precision=2,
        treat_initial_constant_as_zero=True,
        initial_constant_value=0.5,
        initial_constant_tolerance=1e-6,
        initial_constant_max_frames=200  # 필요시 조정
    )

    # 브레이크
    b_count, b_starts, b_durs, b_max = count_pedal_presses(
        df, pedal_col='브레이크 세기', **common_kwargs
    )

    # 엑셀
    a_count, a_starts, a_durs, a_max = count_pedal_presses(
        df, pedal_col='엑셀 세기', **common_kwargs
    )

    # print("=== 브레이크 ===")
    # print("총 브레이크 밟은 횟수:", b_count)
    # print("브레이크 시작 시각 (전체):", b_starts)
    # print("브레이크 밟기 지속 시간 (초, 소수 둘째자리):", [f"{d:.2f}" for d in b_durs])
    # print("브레이크 구간별 최대 브레이크 세기:", [f"{m:.2f}" for m in b_max])

    # print("\n=== 엑셀 ===")
    # print("총 엑셀 밟은 횟수:", a_count)
    # print("엑셀 시작 시각 (전체):", a_starts)
    # print("엑셀 밟기 지속 시간 (초, 소수 둘째자리):", [f"{d:.2f}" for d in a_durs])
    # print("엑셀 구간별 최대 엑셀 세기:", [f"{m:.2f}" for m in a_max])

    # ---- 추가: 통계 출력 (min, max, avg, std) ----
    def _print_stats(label: str, values: List[float]):
        if not values:
            print(f"{label} -> count=0 (데이터 없음)")
            return
        mn = min(values)
        mx = max(values)
        avg = statistics.mean(values)
        # 표본 표준편차 (n>1), n==1 이면 0
        std = statistics.stdev(values) if len(values) > 1 else 0.0
        print(f"{label} -> count={len(values)}, min={mn:.4f}, max={mx:.4f}, avg={avg:.4f}, std={std:.4f}")

    

    print("\n=== 브레이크 통계 ===")
    print(_get_brake_pedal_stats(file_path))
    # _print_stats("브레이크 지속시간(s)", b_durs)
    # _print_stats("브레이크 최대세기", b_max)

    print("\n=== 엑셀 통계 ===")
    print(_get_accel_pedal_stats(file_path))
    # _print_stats("엑셀 지속시간(s)", a_durs)
    # _print_stats("엑셀 최대세기", a_max)
