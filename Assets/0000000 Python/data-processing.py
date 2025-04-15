# ***********************************************************************
# 이 프로그램은 특정 시간구간의 이벤트별 안정감UX 지원 유무에 따른 GSR과 PPG
# 데이터의 통계량을 자동으로 추출해주는 프로그램입니다.
# User 폴더 아래에 User이름-support-o 또는 User이름-support-x 형태의 파일들에
# 있는 데이터의 통계량을 통계분석이 가능한 형태로 정리하여 data-processing-1.csv 의
# 파일 형태로 정리해줍니다.
#                                            @Huhn Kim & Jiwon Shin, 2024
# ***********************************************************************

import os, glob, re
import pandas as pd
from collections import Counter
import numpy as np
from scipy.signal import find_peaks
import matplotlib.pyplot as plt
from datetime import datetime, timedelta
import heartpy as hp  # PPG 통계량 계산
from scipy.signal import butter, filtfilt

# 폴더 경로 설정
gsr_folder = os.path.expanduser("processed_imf3_GSRoutput/")  # 사용자별 GSR 및 표정, 눈동자 데이터
event_folder = os.path.expanduser("User_event_1/")  # 사용자별 이벤트의 시작 및 끝 시간 데이터
driving_folder = os.path.expanduser("User_driving_1/")  # 속도, 핸들각도, 브레이킹 데이터


# GSR 데이터 읽기
def read_csv_with_encoding(file_path, header='infer'):
    if os.path.getsize(file_path) == 0:  # 파일 크기 확인
        print(f"Warning: {file_path} is empty.")
        return pd.DataFrame()  # 빈 데이터프레임 반환

    encodings = ['utf-8', 'ISO-8859-1', 'cp949']
    for enc in encodings:
        try:
            return pd.read_csv(file_path, encoding=enc, header=header)
        except UnicodeDecodeError:
            continue
    raise ValueError(f"Cannot read file {file_path} with available encodings")


gsr_files = glob.glob(os.path.join(gsr_folder, "*.csv"))  # GSR 데이터 읽어와서 데이터베이스화
gsr_data = pd.concat([read_csv_with_encoding(f) for f in gsr_files if not read_csv_with_encoding(f).empty],
                     ignore_index=True)

driving_files = glob.glob(os.path.join(driving_folder, "*.csv"))  # 운전 데이터 읽어와서 데이터베이스화
driving_data = pd.concat([read_csv_with_encoding(f) for f in driving_files if not read_csv_with_encoding(f).empty],
                         ignore_index=True)


# 시간 컬럼에서 밀리초 제거
gsr_data['time'] = gsr_data['time'].apply(lambda x: x.split('.')[0])
driving_data['time'] = driving_data['time'].apply(lambda x:
    datetime.strptime(str(x).split('.')[0], '%I:%M:%S %p' if 'AM' in str(x) or 'PM' in str(x) else '%H:%M:%S').strftime('%H:%M:%S')
    if pd.notnull(x) else None)


# User event 데이터 처리
event_files = glob.glob(os.path.join(event_folder, "*.csv"))  # 이벤트 데이터
output_data = []


# 이벤트 구간 내 max 값에서, 그 이후의 min 값 사이의 시간간격 (대략적인 안정화에 걸린 시간으로 간주)
def time_to_stable(event_data, data_type):
    # Copy data frame and reset index
    event_data = event_data.copy()
    event_data = event_data.reset_index(drop=True)

    # Detect local maxima (peaks) in the data
    max_value = event_data[data_type].max()
    max_index = event_data[event_data[data_type] == max_value].index[0]

    # Extract data after the max value
    post_max_data = event_data.iloc[(max_index + 1):]

    # Handle when there is no data after the max value
    if post_max_data.empty:
        print("No data after the max value.")
        return None

    # Find the min value after the max value
    min_value = post_max_data[data_type].min()
    min_index = post_max_data[post_max_data[data_type] == min_value].index[0]

    # Calculate the time interval between the max value and the min value
    time_to_min = event_data['time(ms)'][min_index] - event_data['time(ms)'][max_index]

    return max_index, min_index, time_to_min


# SCR-z 값이 2이상 (97.7% 불안 확률)인 시간의 합 구하기
def total_time_below_threshold(event_data, a):
    """
    여러 구간에서 SCR 값이 특정 값 'a' 이하인 시간의 총합을 계산하는 함수
    """

    # 데이터프레임을 복사하고 인덱스를 리셋
    event_data = event_data.copy()
    event_data = event_data.reset_index(drop=True)

    # SCR 값이 'a' 이상인 구간에 대한 bool mask 생성
    below_threshold_mask = event_data['SCR_z'] >= a

    # 구간이 여러 개일 수 있으므로 연속된 구간을 그룹으로 묶기
    total_time_below = 0
    in_below_section = False  # 구간 내에 있는지 여부를 추적하는 플래그

    for i in range(1, len(event_data)):
        if below_threshold_mask[i] and not below_threshold_mask[i - 1]:
            # 새로운 'a 이하' 구간이 시작된 시점
            start_time = event_data['time(ms)'].iloc[i]
            in_below_section = True
        elif not below_threshold_mask[i] and below_threshold_mask[i - 1] and in_below_section:
            # 'a 이하' 구간이 끝난 시점
            end_time = event_data['time(ms)'].iloc[i - 1]
            total_time_below += end_time - start_time
            in_below_section = False

    # 마지막 구간이 데이터의 끝에서 끝난 경우 처리
    if in_below_section:
        end_time = event_data['time(ms)'].iloc[-1]
        total_time_below += end_time - start_time

    return total_time_below

def extract_event_number(event_str):  # 문자열에서 숫자를 추출하여 반환하는 기능
    match = re.search(r'\d+', event_str)
    return int(match.group()) if match else None


# 거리 계산 함수 추가
def calculate_distance(x1, y1, x2, y2):
    return np.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)


# 저역 필터 설정 (주파수와 샘플링 레이트는 데이터에 맞게 설정)
def butter_lowpass_filter(data, cutoff, fs, order=5):
    nyq = 0.5 * fs
    normal_cutoff = cutoff / nyq
    b, a = butter(order, normal_cutoff, btype='low', analog=False)

    # 데이터 길이와 비교하여 padlen을 설정
    padlen = min(len(data) - 1, 15)  # 데이터를 고려하여 적절히 설정
    y = filtfilt(b, a, data, padlen=padlen)
    return y


for event_file in event_files:
    user_df = read_csv_with_encoding(event_file, header=None)


    # 열 개수 확인 및 열 이름 설정
    print(f"\nReading file: {event_file}")
    # print(f"Initial columns: {user_df.columns.tolist()}")
    print(f"Number of columns: {len(user_df.columns)}, Number of rows: {len(user_df)}\n")

    if user_df.empty:  # 파일이 비어있으면 건너뜀
        print(f"Skipping empty user file: {event_file}")
        continue

    # 열 이름을 동적으로 설정
    expected_columns = ["date", "time", "event"]
    if len(user_df.columns) >= len(expected_columns):
        user_df.columns = expected_columns + [f"extra_{i}" for i in range(len(user_df.columns) - len(expected_columns))]
    else:
        print(f"Unexpected number of columns in file: {event_file}. Columns: {user_df.columns}")
        continue

    user_df['time'] = user_df['time'].apply(lambda x: str(x).split('.')[0] if pd.notnull(x) else None)  # 시간에서 밀리초 제거

    user_info = os.path.basename(event_file).split('-')
    if len(user_info) < 2:
        print(f"!! Filename format is incorrect for file {event_file}: {user_info}")
        continue

    try:
        user_number = int(user_info[0][1:])  # 파일이름 User 뒤의 숫자 (UserID)
        support_status = user_info[2][0]  # support- 뒤의 o 또는 x
        if (user_number in [1, 2, 3, 4, 5, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20] and support_status == "o") or (
                user_number in [6, 7, 8, 9, 10, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31] and support_status == "x"):
            trial_no = 1
        else:
            trial_no = 2

    except (IndexError, ValueError):
        print(f"!! Error parsing user info from filename {event_file}: {e}")
        continue


    # 이벤트 쌍 처리
    for i in range(0, len(user_df) - 1, 2):
        try:
            start_date = user_df.iloc[i]['date']
            start_time = user_df.iloc[i]['time']
            end_time = user_df.iloc[i + 1]['time']

            # 날짜로 변환하여 시간차이 게산 (형식은 실제 시간 문자열에 맞추어 변경)
            event_period = datetime.strptime(end_time, "%H:%M:%S") - datetime.strptime(start_time, "%H:%M:%S")
            event_period = event_period.total_seconds()

            event_number = extract_event_number(user_df.iloc[i]['event'])

            if event_number is None:
                print(f"!! Invalid event format in file {event_file}: {user_df.iloc[i]['event']}\n")
                continue

            # GSR의 time lag를 고려한 새로운 start_time (GSR 데이터의 지연시간을 고려하여 넉넉하게 10초)
            gsr_time_lag = 10
            start_time = datetime.strptime(start_time, '%H:%M:%S')
            new_start_time = start_time - timedelta(seconds=gsr_time_lag)
            new_start_time_str = new_start_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)

            end_time = datetime.strptime(end_time, '%H:%M:%S')
            new_end_time = end_time + timedelta(seconds=gsr_time_lag)
            new_end_time_str = new_end_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)
            print(start_date, ", ", new_start_time_str, ", ", new_end_time_str)
            #######################################

            event_data = gsr_data[(gsr_data['date'] == start_date) & (gsr_data['time'] >= new_start_time_str) & (
                    gsr_data['time'] <= new_end_time_str)]  # 이벤트 조건, GSR 등 데이터
            event_data = event_data.reset_index(drop=True)

            event_data2 = driving_data[
                (driving_data['date'] == start_date) & (driving_data['time'] >= new_start_time_str) & (
                        driving_data['time'] <= new_end_time_str)]  # 이벤트 조건, 속도, 핸들, 브레이킹
            event_data2 = event_data2.reset_index(drop=True)

            print(f"\nReading... {user_df.iloc[i]['event']}")

            if len(event_data) == 0:  # 조건에 맞는 데이터가 하나도 없으면,
                print(f"!! No valid event data in file {event_file}: {user_df.iloc[i]['event']}\n")
                continue

            if len(event_data2) == 0:  # 조건에 맞는 데이터가 하나도 없으면,
                print(f"!! No valid driving data {new_start_time_str} ~ {new_end_time_str}\n")
                continue

            speed_avg = event_data2['speed'].mean() if not event_data2['speed'].empty else None
            handle_avg = event_data2['handle'].mean() if not event_data2['handle'].empty else None
            brake_avg = event_data2['brake'].mean() if not event_data2['brake'].empty else None
            speed_std = event_data2['speed'].std() if not event_data2['speed'].empty else None
            handle_std = event_data2['handle'].std() if not event_data2['handle'].empty else None
            brake_std = event_data2['brake'].std() if not event_data2['brake'].empty else None
            brake_feq = event_data2['brake'][event_data2['brake'] > 0].count() if not event_data2[
                'brake'].empty else None  #브레이킹 빈도 수

            gsr_avg = event_data['GSR'].mean() if not event_data['GSR'].empty else None
            gsr_std = event_data['GSR'].std() if not event_data['SCR'].empty else None
            gsr_max = event_data['GSR'].max() if not event_data['SCR'].empty else None
            gsr_min = event_data['GSR'].min() if not event_data['SCR'].empty else None
            gsr_range = gsr_max - gsr_min
            gsr_peaks = len(find_peaks(event_data['GSR'].tolist())[0])

            gsr_z_avg = event_data['GSR_z'].mean() if not event_data['GSR_z'].empty else None
            scr_z_avg = event_data['SCR_z'].mean() if not event_data['SCR_z'].empty else None

            # result = time_to_stable(event_data, 'GSR', 0.5, gsr_avg+gsr_std) # gsr 평균-1시그마를 안정화 기준으로
            # gsr_time_to_stable = None

            # 이벤트 구간별 scatter 차트 그려서 확인해보기
            # plt.figure(figsize=(10, 6))
            # plt.plot(event_data['time(ms)'], event_data['GSR'], marker='o', linestyle='-', color='b')
            # if result is not None:
            #     first_bounce_index, zero_index, gsr_time_to_stable = result
            #     print(f"Time to GSR Stabilization: {gsr_time_to_stable}")

            # 시각화: 튀는 점과 안정화 시점을 표시
            # plt.axvline(x=event_data['time(ms)'][first_bounce_index], color='r', linestyle='--',
            #             label='First Bounce Point')
            # plt.axvline(x=event_data['time(ms)'][zero_index], color='g', linestyle='--',
            #             label='Stabilization Point')
            # else:
            #     print("Could not calculate the time to GSR stabilization.")

            # plt.title('GSR Values Over Time with Bounce and Stabilization Points')
            # plt.xlabel('Time (ms)')
            # plt.ylabel('GSR Value')
            # plt.legend()
            # plt.grid(True)
            # plt.show()
            #######################################

            scr_avg = event_data['SCR'].mean() if not event_data['SCR'].empty else None
            scr_std = event_data['SCR'].std() if not event_data['SCR'].empty else None
            scr_max = event_data['SCR'].max() if not event_data['SCR'].empty else None
            scr_min = event_data['SCR'].min() if not event_data['SCR'].empty else None
            scr_range = scr_max - scr_min
            scr_peaks = len(find_peaks(event_data['SCR'].tolist())[0])
            # 안정화 duration 계산: 정확한지는 의문 ???

            result = time_to_stable(event_data, 'GSR')
            gsr_time_to_stable = None

            # 이벤트 구간별 scatter 차트 그려서 확인해보기
            #plt.figure(figsize=(10, 6))
            #plt.plot(event_data['time(ms)'], event_data['SCR_z'], marker='o', linestyle='-', color='b')
            if result is not None:
                max_index, min_index, gsr_time_to_stable = result
                print(f"Time to SCR Stabilization: {gsr_time_to_stable}")

                if max_index is not None and min_index is not None:
                    if max_index < len(event_data) and min_index < len(event_data):
                        # 시각화: 튀는 점과 안정화 시점을 표시
                        plt.axvline(x=event_data['time(ms)'][max_index], color='r', linestyle='--',
                                    label='First Bounce Point')
                        plt.axvline(x=event_data['time(ms)'][min_index], color='g', linestyle='--',
                                    label='Stabilization Point')
                    else:
                        print("Error: Calculated indices are out of bounds.")
            else:
                print("Could not calculate the time to SCR stabilization.")

            # plt.title('SCR Values Over Time with Bounce and Stabilization Points')
            # plt.xlabel('Time (ms)')
            # plt.ylabel('SCR Value')
            # plt.legend()
            # plt.grid(True)
            # plt.show()
            #######################################

            scr_duration_unstable = total_time_below_threshold(event_data, 2)  # scr z값이 2이상인 시간 합

            ppg_avg = event_data['PPG'].mean() if not event_data['PPG'].empty else None
            ppg_std = event_data['PPG'].std() if not event_data['PPG'].empty else None
            ppg_max = event_data['PPG'].max() if not event_data['PPG'].empty else None
            ppg_min = event_data['PPG'].min() if not event_data['PPG'].empty else None
            ppg_range = ppg_max - ppg_min
            ppg_peaks = len(find_peaks(event_data['PPG'].tolist())[0])

            ############### PPG 통계량 계산 @@@@@@@@@@@@@@@@
            # 필터 적용 (5Hz 샘플링, 2Hz 필터 컷오프 예시)
            filtered_ppg = butter_lowpass_filter(event_data['PPG'], cutoff=2, fs=5)

            HR, HRV, SDNN, RMSSD, pNN50 = None, None, None, None, None
            if filtered_ppg.size != 0:
                try:
                    wd, m = hp.process(filtered_ppg, 5)
                    # for measure in m.keys():
                    #     print('%s: %f' % (measure, m[measure]))
                    HR = m['bpm']  # 1분(60초)동안 몇 번의 심장박출이 일어났는 가(심장이 뛰었는가)를 계산
                    HRV = m['ibi']  # inter-beat interval, R-피크 사이의 시간간격
                    SDNN = m['sdnn']  # Standard deviation of all NN intervals, 전체적인 심박변이도의 추이를 반영
                    RMSSD = m['rmssd']  # Root Mean Square of Successive Differences, 연속적인 NN 간격 차이의 제곱근 평균. 낮은 RMSSD는 부교감 신경계의 활동 감소를 나타내며, 스트레스 상태일 수 있음을 의미 (단기 HRV)
                    pNN50 = m['pnn50']  # Changes in successive normal sinus (NN) intervals exceeding 50 ms (pNN50), expressed as a percentage. Like other HRV measures, pNN50 also indicates the overall activity of the autonomic nervous system
                    # 심박의 간격을 일컫는 용어는 일반적으로 NN 간격 (normal-to-normal interval, NN inteval)

                except hp.exceptions.BadSignalWarning as e:
                    print("Warning: Bad signal detected. Please check the PPG data.")
                    # 추가적인 예외 처리 로직 작성
            else:
                print("No PPG data available.")

            # 눈 데이터 처리
            leye_distances = []
            reye_distances = []
            leye_blink = reye_blink = eye_blink = 0 #눈깜빡임 횟수

            for j in range(1, len(event_data)-1):
                leye_distances.append(calculate_distance(
                    event_data.iloc[j - 1]['Leye_X'], event_data.iloc[j - 1]['Leye_Y'],
                    event_data.iloc[j]['Leye_X'], event_data.iloc[j]['Leye_Y']
                ))
                reye_distances.append(calculate_distance(
                    event_data.iloc[j - 1]['Reye_X'], event_data.iloc[j - 1]['Reye_Y'],
                    event_data.iloc[j]['Reye_X'], event_data.iloc[j]['Reye_Y']
                ))

                ### 101 패턴을 찾아서 blink rate를 계산
                if event_data.iloc[j - 1]['Leye_open'] == 1 and event_data.iloc[j]['Leye_open'] == 0: #뜨고 있다 감은 경우만 체크
                    leye_blink += 1
                if event_data.iloc[j - 1]['Reye_open'] == 1 and event_data.iloc[j]['Reye_open'] == 0:
                    reye_blink += 1
                if ((event_data.iloc[j - 1]['Reye_open'] == 0 and event_data.iloc[j]['Reye_open'] == 1)
                        or (event_data.iloc[j - 1]['Leye_open'] == 0 and event_data.iloc[j]['Leye_open'] == 1)):
                    eye_blink += 1

            leye_dist_avg = np.mean(leye_distances) if leye_distances else None
            reye_dist_avg = np.mean(reye_distances) if reye_distances else None

            leye_open_avg = event_data['Leye_open'].mean() if not event_data['Leye_open'].empty else None
            reye_open_avg = event_data['Reye_open'].mean() if not event_data['Reye_open'].empty else None

            emotions = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']
            emotion_data = event_data[emotions]
            emotion_counts = Counter(emotion_data.idxmax(axis=1))
            most_common_emotion = emotion_counts.most_common(1)[0][0] if emotion_counts else None
            second_common_emotion = \
                Counter(emotion_data.apply(lambda x: x.nlargest(2).idxmin(), axis=1)).most_common(1)[0][
                    0] if not emotion_data.empty else None

            avg_emotions = {emotion: event_data[emotion].mean() if not event_data[emotion].empty else None for emotion
                            in emotions}

            output_data.append([
                user_number, support_status, event_number, trial_no, event_period,
                speed_avg, handle_avg, brake_avg, speed_std, handle_std, brake_std, brake_feq,
                gsr_avg, gsr_std, gsr_max, gsr_min, gsr_range, gsr_peaks, gsr_z_avg,
                scr_avg, scr_std, scr_max, scr_min, scr_range, gsr_time_to_stable, scr_duration_unstable, scr_peaks,
                scr_z_avg,
                ppg_avg, ppg_std, ppg_max, ppg_min, ppg_range, ppg_peaks, HR, HRV, SDNN, RMSSD, pNN50,
                leye_dist_avg, reye_dist_avg, leye_open_avg, reye_open_avg, leye_blink/event_period if event_period != 0 else 0, reye_blink/event_period if event_period != 0 else 0, eye_blink/event_period if event_period != 0 else 0,
                avg_emotions['Angry'], avg_emotions['Disgust'], avg_emotions['Fear'],
                avg_emotions['Happy'], avg_emotions['Neutral'], avg_emotions['Sad'],
                avg_emotions['Surprise'], most_common_emotion, second_common_emotion
            ])
        except IndexError as e:
            print(f"IndexError while processing file {event_file}: {e}")

# 데이터프레임으로 변환
columns = [
    'User', 'Support', 'Event', 'Trial_no', 'Event_period',
    "Speed_avg", "Handle_avg", "Brake_avg", "Speed_std", "Handle_std", "Brake_std", "Brake_feq",
    'GSR_avg', 'GSR_std', 'GSR_max', 'GSR_min', 'GSR_range', 'GSR_peaks', "GSR_Z_avg",
    'SCR_avg', 'SCR_std', 'SCR_max', 'SCR_min', 'SCR_range', 'GSR_time_to_stable', 'SCR_duration_unstable', 'SCR_peaks',
    "SCR_Z_avg",
    'PPG_avg', 'PPG_std', 'PPG_max', 'PPG_min', 'PPG_range', 'PPG_peaks', 'HR_bpm', 'HRV_ibi', 'SDNN', 'RMSSD', 'pNN50',
    'Leye_dist_avg', 'Reye_dist_avg', 'Leye_open_avg', 'Reye_open_avg', 'Leye_blink_rate', 'Reye_blink_rate', 'eye_blink_rate',
    'Angry_avg', 'Disgust_avg', 'Fear_avg', 'Happy_avg', 'Neutral_avg', 'Sad_avg', 'Surprise_avg', 'Emotion1',
    'Emotion2'
]
output_df = pd.DataFrame(output_data, columns=columns)

# 1,000,000 행씩 나누어 파일로 저장
rows_per_file = 1000000
file_count = 1

for i in range(0, len(output_df), rows_per_file):
    output_df.iloc[i:i + rows_per_file].to_csv(f'data-processing-result-{file_count}.csv', index=False)
    file_count += 1

print("Data processing complete.")
