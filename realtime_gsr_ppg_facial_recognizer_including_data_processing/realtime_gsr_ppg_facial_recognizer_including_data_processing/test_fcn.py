import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.signal import find_peaks

# CSV 파일에서 데이터 읽기 (Excel 형식이므로 .csv 확장자 사용)
file_path = 'GSRoutput/GSR_data_2024-10-14_17h_49m.csv'  # 파일 경로를 지정하세요.
data = pd.read_csv(file_path)

data = data[(data['time(ms)'] >= 1728895817599) & (data['time(ms)'] <= 1728895959987)]  # 이벤트 조건
data = data.reset_index(drop=True)

# SCR 열 추출if 'SCR' in data.columns:
if 'SCR' in data.columns:
    scr_data = data['SCR']
    time_data = data['time(ms)']
    print("SCR data successfully loaded.")
else:
    print("SCR column not found in the file.")

# 데이터 확인 (선택 사항)print(scr_data.head())

# 새로운 데이터프레임 생성
sample_data = pd.DataFrame({
    'time(ms)': time_data,
    'SCR': scr_data
})

# 튀는 점과 안정화 시점 계산을 위해 함수 실행

def time_to_stable(event_data, threshold=0.03):
    # Copy data frame and reset index
    event_data = event_data.copy()
    event_data = event_data.reset_index(drop=True)

    # Detect peaks (local maxima) in the SCR values
    peaks, _ = find_peaks(event_data['SCR'], height=threshold)
    print(peaks)

    if len(peaks) == 0:
        print("No peaks found.")
        return None

    # The first peak and the last peak indices
    first_peak_index = peaks[0]
    last_peak_index = peaks[-1]

    # Find the point before the first peak where SCR becomes greater than 0
    pre_first_peak_data = event_data.iloc[:first_peak_index]
    first_positive_index = pre_first_peak_data[pre_first_peak_data['SCR'] > threshold].index[0]

    # Extract data after the last peak
    post_last_peak_data = event_data.iloc[(last_peak_index + 1):]

    if post_last_peak_data.empty:
        print("No data after the last peak.")
        return None

    # Find the point after the last peak where SCR becomes less than 0
    zero_indices = post_last_peak_data[post_last_peak_data['SCR'] < 0].index

    if zero_indices.empty:
        print("No zero or negative SCR value found after the last peak.")
        return None

    # The index at which the first point falls below 0 after the last peak
    last_negative_index = zero_indices[0]

    # Calculate the time from the first point where SCR > 0 (before the first peak) to the point where SCR < 0 (after the last peak)
    time_to_stable = event_data['time(ms)'][last_negative_index] - event_data['time(ms)'][first_positive_index]

    return first_positive_index, last_negative_index, time_to_stable

# 함수 실행 및 튀는 점과 안정화 시점 계산
result = time_to_stable(sample_data)

if result is not None:
    first_bounce_index, zero_index, time_to_stable_result = result
    print(f"First Bounce Index: {first_bounce_index}")
    print(f"Zero Index: {zero_index}")
    print(f"Time to Stabilization: {time_to_stable_result}")
    if first_bounce_index is not None and zero_index is not None:
        if first_bounce_index < len(sample_data) and zero_index < len(sample_data):

            # 시각화: 튀는 점과 안정화 시점을 표시
            plt.figure(figsize=(10, 6))
            plt.plot(sample_data['time(ms)'], sample_data['SCR'], marker='o', linestyle='-', color='b')
            plt.axvline(x=sample_data['time(ms)'][first_bounce_index], color='r', linestyle='--',
                        label='First Bounce Point')
            plt.axvline(x=sample_data['time(ms)'][zero_index], color='g', linestyle='--', label='Stabilization Point')
            plt.title('SCR Values Over Time with Bounce and Stabilization Points')
            plt.xlabel('Time (ms)')
            plt.ylabel('SCR Value')
            plt.legend()
            plt.grid(True)
            plt.show()
        else:
            print("Error: Calculated indices are out of bounds.")

else:
    print("Could not calculate the time to stabilization.")



