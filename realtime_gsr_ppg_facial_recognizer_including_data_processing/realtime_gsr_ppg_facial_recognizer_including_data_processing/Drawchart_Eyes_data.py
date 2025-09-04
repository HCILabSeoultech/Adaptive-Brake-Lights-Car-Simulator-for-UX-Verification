# ***********************************************************************
# 이 프로그램은 사용자별 눈동자움직임 데이터를 이벤트 구간별로 그래프를 그려준다
#                                                           @Huhn Kim, 2024
# ***********************************************************************

import os, glob, re
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.font_manager as fm
from datetime import datetime, timedelta

# 한글 폰트 설정 (예: 나눔고딕)
# 'Gulim' 폰트의 경로를 설정 (시스템에 설치된 경로로 변경)
font_path = 'Font/malgun.ttf'  # Windows의 경우 보통 C:\Windows\Fonts 안에 있습니다.
font_prop = fm.FontProperties(fname=font_path)

# 폴더 경로 설정
gsr_folder = os.path.expanduser("GSRoutput/")
user_folder = os.path.expanduser("User_event/")

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


gsr_files = glob.glob(os.path.join(gsr_folder, "*.csv"))
gsr_data = pd.concat([read_csv_with_encoding(f) for f in gsr_files if not read_csv_with_encoding(f).empty],
                     ignore_index=True)

# 시간 컬럼에서 밀리초 제거
gsr_data['time'] = gsr_data['time'].apply(lambda x: x.split('.')[0])

# User 데이터 처리
user_files = glob.glob(os.path.join(user_folder, "*.csv"))
output_data = []

gsr_time_lag = 10 # GSR 데이터의 타임 렉 수준 정의
event_name = ["슬픔", "세찬비", "과속100", "과속50", "느린차", "타이어", "끼어들기1", "우회전1", "터널전", "터널후", "오르막",
              "우회전2", "구급차", "스포츠카", "끼어들기2", "차폭1", "차폭2"]
def extract_event_number(event_str):  # 문자열에서 숫자를 추출하여 반환하는 기능
    match = re.search(r'\d+', event_str)
    return int(match.group()) if match else None

# 시간 형식을 초로 변환하는 함수
def time_to_seconds(time_str):
    time_obj = datetime.strptime(time_str, '%H:%M:%S')
    return time_obj.hour * 3600 + time_obj.minute * 60 + time_obj.second

# 시:분:초로 변환하는 함수
def seconds_to_time(seconds):
    hours = seconds // 3600
    minutes = (seconds % 3600) // 60
    seconds = seconds % 60
    return f"{hours:02}:{minutes:02}:{seconds:02}"

for user_file in user_files:
    user_df = read_csv_with_encoding(user_file, header=None)

    # 열 개수 확인 및 열 이름 설정
    print(f"\nReading file: {user_file}")
    # print(f"Initial columns: {user_df.columns.tolist()}")
    print(f"Number of columns: {len(user_df.columns)}, Number of rows: {len(user_df)}\n")

    if user_df.empty:  # 파일이 비어있으면 건너뜀
        print(f"Skipping empty user file: {user_file}")
        continue

    # 열 이름을 동적으로 설정
    expected_columns = ["date", "time", "event"]
    if len(user_df.columns) >= len(expected_columns):
        user_df.columns = expected_columns + [f"extra_{i}" for i in range(len(user_df.columns) - len(expected_columns))]
    else:
        print(f"Unexpected number of columns in file: {user_file}. Columns: {user_df.columns}")
        continue

    user_df['time'] = user_df['time'].apply(lambda x: str(x).split('.')[0] if pd.notnull(x) else None)  # 시간에서 밀리초 제거

    user_info = os.path.basename(user_file).split('-')
    if len(user_info) < 2:
        print(f"!! Filename format is incorrect for file {user_file}: {user_info}")
        continue

    try:
        user_number = int(user_info[0][1:])  # 파일이름 User 뒤의 숫자 (UserID)
        support_status = user_info[2][0]  # support- 뒤의 o 또는 x
    except (IndexError, ValueError):
        print(f"!! Error parsing user info from filename {user_file}: {e}")
        continue

    start_date = user_df.iloc[0]['date']
    start_time = user_df.iloc[0]['time']
    end_time = user_df.iloc[-1]['time']

    # 이벤트 시간 범위 내의 데이터 필터링
    event_data = gsr_data[(gsr_data['date'] == start_date) &
                          (gsr_data['time'] >= start_time) &
                          (gsr_data['time'] <= end_time)]

    print(f"{start_date}: {start_time} ~ {end_time} 까지의 gsr data")

    # event_data['time']을 초로 변환하여 새로운 열을 추가
    event_data['time_seconds'] = event_data['time'].apply(time_to_seconds).copy()  # .copy() 추가

    plt.figure(figsize=(21, 7))
    plt.title(f'{user_file}: Eyes data')
    plt.gca().yaxis.set_visible(False)  # Hide x-axis labels
    plt.gca().xaxis.set_visible(False)  # Hide x-axis labels

    # # x축 범위 설정 (서로 동일하게 설정)
    x_min = min(event_data['time_seconds'])
    x_max = max(event_data['time_seconds'])

    # 첫 번째 서브플롯 - GSR 값
    plt.subplot(2, 1, 1)
    plt.plot(event_data['time_seconds'], event_data['Leye_X'], linestyle='-', color='g')  # 마커 없이 선만 그리기
    plt.ylabel('Leye_X', labelpad=15)

    # y축 범위 자동 조정 또는 수동으로 설정
    plt.ylim(min(event_data['Leye_X']), max(event_data['Leye_X']))  # 빈 공간 최소화
    plt.xlim(x_min, x_max)  # x축 범위를 동일하게 설정

    # y=3에 수평선 그리기
    plt.axhline(y=2, color='gray', linestyle='-', linewidth=0.5) #GSR은 2시그마, 97.7%

    # 이벤트 쌍 처리 (1번부터 17번까지 레이블 부여)
    for i in range(0, len(user_df) - 1, 2):
        event_start_time = user_df.iloc[i]['time']
        event_end_time = user_df.iloc[i + 1]['time']

        # GSR의 time lag를 고려한 새로운 start_time (GSR 데이터의 지연시간을 고려하여 넉넉하게 10초)
        start_time = datetime.strptime(event_start_time, '%H:%M:%S')
        new_start_time = start_time - timedelta(seconds=gsr_time_lag)
        new_start_time_str = new_start_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)
        event_start_time_seconds = time_to_seconds(event_start_time)

        end_time = datetime.strptime(event_end_time, '%H:%M:%S')
        new_end_time = end_time + timedelta(seconds=gsr_time_lag)
        new_end_time_str = new_end_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)
        event_end_time_seconds = time_to_seconds(new_end_time_str)
        #######################################

        event_label = extract_event_number(user_df.iloc[i]['event'])

        # 이벤트 시작 시간에 빨간 수직선 그리기
        plt.axvline(x=event_start_time_seconds, color='r', linestyle='--', linewidth=0.8)

        # 이벤트 종료 시간에 초록 수직선 그리기
        plt.axvline(x=event_end_time_seconds, color='g', linestyle='--', linewidth=0.8)

    # Suppress x-axis for first subplot
    plt.gca().xaxis.set_visible(False)  # Hide x-axis labels

    # 두 번째 서브플롯 - SCR 값
    ax1 = plt.subplot(2, 1, 2)
    ax1.plot(event_data['time_seconds'], event_data['Leye_Y'], linestyle='-', color='b')  # 마커 없이 선만 그리기
    ax1.set_ylabel('Leye_Y', labelpad=15)

    # y축 범위 조정
    plt.ylim(min(event_data['Leye_Y']), max(event_data['Leye_Y']))  # 빈 공간 최소화
    plt.xlim(x_min, x_max)  # x축 범위를 동일하게 설정

    # x축 레이블 sparsely 표시하기
    x_ticks = ax1.get_xticks()  # 기존 x축 눈금 가져오기
    # ax1.set_xticks(x_ticks[::5])  # 5개 간격으로 x축 눈금 설정

    # x축 레이블을 초에서 시:분:초로 변환하여 설정
    ax1.set_xticklabels([seconds_to_time(int(tick)) for tick in x_ticks[::1]])

    # y=3에 빨간색 수평선 그리기
    plt.axhline(y=3, color='gray', linestyle='-', linewidth=0.5) #SCR은 3시그마, 99.9% 불안할 확률

    # 동일한 이벤트 번호를 x축 아래에 표시
    for i in range(0, len(user_df) - 1, 2):
        event_start_time = user_df.iloc[i]['time']
        event_end_time = user_df.iloc[i + 1]['time']

        # GSR의 time lag를 고려한 새로운 start_time (GSR 데이터의 지연시간을 고려하여 넉넉하게 10초)
        start_time = datetime.strptime(event_start_time, '%H:%M:%S')
        new_start_time = start_time - timedelta(seconds=gsr_time_lag)
        new_start_time_str = new_start_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)
        event_start_time_seconds = time_to_seconds(event_start_time)

        end_time = datetime.strptime(event_end_time, '%H:%M:%S')
        new_end_time = end_time + timedelta(seconds=gsr_time_lag)
        new_end_time_str = new_end_time.time().strftime('%H:%M:%S')  # '시간:분:초' 형식으로 변환 (연도, 월, 일 정보는 포함되지 않음)
        event_end_time_seconds = time_to_seconds(new_end_time_str)
        #######################################

        event_label = extract_event_number(user_df.iloc[i]['event'])

        # 이벤트 시작 시간에 빨간 수직선 그리기
        plt.axvline(x=event_start_time_seconds, color='r', linestyle='--', linewidth=0.8)

        # 이벤트 종료 시간에 초록 수직선 그리기
        plt.axvline(x=event_end_time_seconds, color='g', linestyle='--', linewidth=0.8)

        # y축 최소값과 최대값
        y_min = min(event_data['Leye_Y'])
        y_max = max(event_data['Leye_Y'])

        # 이벤트 번호를 x축 바로 위에 표시 (y값 조정)
        plt.text(x=event_start_time_seconds, y=y_min - 0.1 * (y_max - y_min), s=str(event_label), color='r',
                 horizontalalignment='center', verticalalignment='top', fontsize=8)
        plt.text(x=event_end_time_seconds, y=y_min - 0.1 * (y_max - y_min), s=str(event_label), color='g',
                 horizontalalignment='center', verticalalignment='top', fontsize=8)

        plt.text(x=event_start_time_seconds, y=y_min - 0.1 * (y_max - y_min + 20), s=str(event_name[event_label-1]), color='black',
                 horizontalalignment='center', verticalalignment='top', fontsize=8, fontproperties=font_prop)

    # Suppress x-axis for first subplot
    # plt.gca().xaxis.set_visible(False)  # Hide x-axis labels

    # 레이아웃을 깔끔하게 조정 (패딩 최소화)
    plt.tight_layout(pad=2.0)  # 패딩 조정
    plt.savefig(f'Eyes_graphs/P{user_number}_{support_status}_eyes.png') #<<---- 이미지 파일로 한번에 저장할 때

    #plt.show() # 이미지 하나씩 바로 보고자 할때
