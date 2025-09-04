import numpy as np
import pandas as pd
from scipy.signal import find_peaks
from scipy.interpolate import CubicSpline
import os
from glob import glob


def find_local_extrema(signal):
    """Find local maxima and minima in the signal"""
    maxima, _ = find_peaks(signal)
    minima, _ = find_peaks(-signal)
    return maxima, minima


def calculate_midpoints(x, signal, maxima, minima):
    """Calculate midpoints between consecutive maxima and minima pairs"""
    # 모든 극값 포인트를 결합하고 정렬
    all_points = np.sort(np.concatenate([maxima, minima]))
    midpoints_x = []
    midpoints_y = []

    for i in range(len(all_points) - 1):
        x1, x2 = x[all_points[i]], x[all_points[i + 1]]
        y1, y2 = signal[all_points[i]], signal[all_points[i + 1]]

        # x1과 x2가 다른 경우에만 중점 추가
        if x1 != x2:
            # 중점 계산
            mid_x = (x1 + x2) / 2
            mid_y = (y1 + y2) / 2

            midpoints_x.append(mid_x)
            midpoints_y.append(mid_y)

    return np.array(midpoints_x), np.array(midpoints_y)


def apply_eia(time, signal, num_iterations=7):
    """Apply Empirical Iterative Algorithm to remove motion artifacts"""
    # 원본 인덱스에 대한 매핑 생성
    orig_indices = np.arange(len(time))

    # NaN 값을 제거하면서 원본 위치 추적
    valid_mask = ~(np.isnan(time) | np.isnan(signal))
    valid_time = time[valid_mask]
    valid_signal = signal[valid_mask]
    valid_indices = orig_indices[valid_mask]

    if len(valid_time) < 3:
        return signal  # 유효한 포인트가 부족한 경우 원본 신호 반환

    # 원본 위치를 추적하면서 데이터 정렬
    sort_idx = np.argsort(valid_time)
    sorted_time = valid_time[sort_idx]
    sorted_signal = valid_signal[sort_idx]
    sorted_indices = valid_indices[sort_idx]

    # 중복 제거
    unique_mask = np.concatenate([[True], np.diff(sorted_time) > 0])
    unique_time = sorted_time[unique_mask]
    unique_signal = sorted_signal[unique_mask]
    unique_indices = sorted_indices[unique_mask]

    if len(unique_time) < 3:
        return signal  # 고유한 포인트가 부족한 경우 원본 신호 반환

    current_signal = unique_signal.copy()

    # EIA 반복 수행
    for i in range(num_iterations):
        # 지역 최대값과 최소값 찾기
        maxima, minima = find_local_extrema(current_signal)

        # 극값이 없으면 반복 중단
        if len(maxima) == 0 or len(minima) == 0:
            break

        # 중점 계산
        mid_x, mid_y = calculate_midpoints(unique_time, current_signal, maxima, minima)

        # 보간에 필요한 포인트가 부족하면 중단
        if len(mid_x) <= 2:
            break

        # 끝점 추가
        if mid_x[0] > unique_time[0]:
            mid_x = np.concatenate([[unique_time[0]], mid_x])
            mid_y = np.concatenate([[current_signal[0]], mid_y])
        if mid_x[-1] < unique_time[-1]:
            mid_x = np.concatenate([mid_x, [unique_time[-1]]])
            mid_y = np.concatenate([mid_y, [current_signal[-1]]])

        # 3차 스플라인 보간 수행
        cs = CubicSpline(mid_x, mid_y)
        current_signal = cs(unique_time)

    # 잔여 신호 계산
    cleaned_unique = unique_signal - current_signal

    # 원본 신호 크기의 출력 배열 초기화
    cleaned_signal = signal.copy()

    # 계산된 위치의 값만 업데이트
    cleaned_signal[valid_indices[sort_idx][unique_mask]] = cleaned_unique

    return cleaned_signal


def calculate_z_score(data):
    """Calculate z-score of the input data"""
    return (data - np.mean(data)) / np.std(data)


def calculate_scr(gsr_signal):
    """Calculate SCR by computing the derivative of GSR signal"""
    # 연속된 값의 차이 계산 (미분)
    scr = np.diff(gsr_signal)
    # 첫 번째 위치의 값을 0으로 패딩하여 원본 길이와 맞춤
    scr = np.pad(scr, (1, 0), 'constant', constant_values=0)
    return scr


def process_gsr_file(input_file, output_dir):
    """Process GSR file and save the cleaned signal"""
    # CSV 파일 읽기
    df = pd.read_csv(input_file)

    # 타임스탬프를 시간 값으로 사용
    time = df.iloc[:, 0].values.astype(float)  # 첫 번째 열은 타임스탬프

    # GSR 신호 가져오기 (5번째 열)
    gsr_signal = df.iloc[:, 4].values.astype(float)

    # EIA 알고리즘 적용
    cleaned_gsr = apply_eia(time, gsr_signal)

    # GSR 관련 열 업데이트
    df.iloc[:, 4] = cleaned_gsr  # GSR
    df.iloc[:, 5] = calculate_z_score(cleaned_gsr)  # GSR_z

    # SCR 계산 및 업데이트
    scr = calculate_scr(cleaned_gsr)
    df.iloc[:, 6] = scr  # SCR
    df.iloc[:, 7] = calculate_z_score(scr)  # SCR_z

    # 출력 파일명 생성
    base_name = os.path.basename(input_file)
    output_name = f"{base_name}"
    output_path = os.path.join(output_dir, output_name)

    # CSV 파일로 저장
    df.to_csv(output_path, index=False)

    return output_path


def main():
    """Main function to control the entire file processing workflow"""
    # 입력 및 출력 디렉토리 설정
    input_dir = "GSR_ma_test/"
    output_dir = "GSR_ma_test/cleaned/"

    # 출력 디렉토리 생성
    os.makedirs(output_dir, exist_ok=True)

    # 모든 CSV 파일 처리
    csv_files = glob(os.path.join(input_dir, "*.csv"))

    for file in csv_files:
        try:
            output_path = process_gsr_file(file, output_dir)
            print(f"파일 처리 완료: {file} -> {output_path}")
        except Exception as e:
            print(f"파일 처리 실패: {file}")
            print(f"오류 내용: {str(e)}")


if __name__ == "__main__":
    main()