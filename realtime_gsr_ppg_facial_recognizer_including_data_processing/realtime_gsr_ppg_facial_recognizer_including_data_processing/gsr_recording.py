###################################################################
# GSR(Skin resistance)/SCR, PPG(mv) 데이터 실시간 뷰어 및 기록 프로그램
# main.py에서 이 코드를 subprocess로 호출하여 실행함
###################################################################

import serial
import statistics
import struct
import time
from datetime import datetime
import cv2
import numpy as np
from pylsl import StreamInfo, StreamOutlet




##################################################################################################
btPort = "COM5"  # 쉬머 블루투스 포트 설정, 컴퓨터에서 "추가 Bluetooth 설정"을 보고 포트 확인 필요 쉬머 장치 바뀌면 바꿔줘야함 안될시 확인할 것 !!!
##################################################################################################

window_moved = False  # 처음에만 윈도우 창 이동위한 플래그
init_positonx, init_positony = 100, 100  # 윈도우 창이 뜨는 초기 위치
sampling_freq = 5  # 표정인식이 5 fps라 거기에 맞춤.
max_values_to_show = sampling_freq * 60 * 1  # 1분 그래프에 표시할 최대 값의 개수

startdate = str(datetime.now().date()) + "_" + str(datetime.now().hour) + "h_" + str(datetime.now().minute) + "m"



def wait_for_ack():
    ddata = ""
    ack = struct.pack('B', 0xff)
    while ddata != ack:
        ddata = ser.read(1)
        # print("0x{}02x".format(ord(ddata[0])))
        # print('{}'.format(ddata[0]))
    return


############################################################
### 쉬머 데이터 읽기 위한 블루투스 세팅
############################################################
ser = serial.Serial(btPort, 115200)  # 115200
ser.flushInput()
print("port opening, done.")

# send the set sensors command
ser.write(struct.pack('BBBB', 0x08, 0x04, 0x01, 0x00))  # GSR and PPG
wait_for_ack()
print("sensor setting, done.")

# Enable the internal expansion board power
ser.write(struct.pack('BB', 0x5E, 0x01))
wait_for_ack()
print("enable internal expansion board power, done.")

# send the set sampling rate command
'''
 sampling_freq = 32768 / clock_wait = X Hz
'''
clock_wait = int((2 << 14) / sampling_freq)

ser.write(struct.pack('<BH', 0x05, clock_wait))
wait_for_ack()

# send start streaming command
ser.write(struct.pack('B', 0x07))
wait_for_ack()
print("start command sending, done.")

# read incoming data
ddata = b''
numbytes = 0
framesize = 8  # 1byte packet type + 3byte timestamp + 2 byte GSR + 2 byte PPG(Int A13)

#  LSL
# Configure a streaminfo
info = StreamInfo(name='Shimmer3', type='gsr_ppg', channel_count=4, channel_format='float32', source_id='uidShimmer')
# next make an outlet
outlet = StreamOutlet(info)
# Configure the shimmer to collect the user defined time period.
# print("Packet Type\tTimestamp\tGSR\tPPG")
############################################################

# //////////////////////////// GSR 및 SCR 그래프 그리기 //////////////////////////////////
# Parameters for the graph
graph_width = 1200  # 그래프 이미지의 너비
graph_height = 440  # 그래프 이미지의 높이
padding = 50  # y축 눈금을 위한 왼쪽 패딩
axis_font_scale = 0.4  # 그래프 축 레이블 크기
last_calculated_index = -sampling_freq  # 마지막 기울기 계산 인덱스 초기화

# Create an empty black image for the graph
graph_img = np.zeros((graph_height + padding, graph_width + padding, 3), dtype=np.uint8)

# 각 데이터 포인트에 대한 타임스탬프 저장
timestamps1 = []
emotion_values1 = []


################ GSR 그래프 ##################
def update_graph_GSR(emotion_value):
    current_time = time.strftime('%H:%M:%S')  # 현재 시간

    # 새 감정 값과 타임스탬프 추가
    emotion_values1.append(emotion_value)
    timestamps1.append(current_time)

    # 리스트의 길이가 max_values_to_show를 초과하면 가장 오래된 값을 제거
    if len(emotion_values1) > max_values_to_show:
        emotion_values1.pop(0)
        timestamps1.pop(0)

    # 현재 최대값과 최소값을 기준으로 y_scale 조정
    max_value = max(emotion_values1) if emotion_values1 else 5
    min_value = min(emotion_values1) if emotion_values1 else 0
    y_range = max_value - min_value if max_value != min_value else 1
    y_scale = graph_height / y_range

    # 그래프 이미지 초기화
    graph_img[:] = 0

    # y축 축선 그리기
    cv2.line(graph_img, (padding, 0), (padding, graph_height), (255, 255, 255), 2)

    # x축 축선 그리기
    cv2.line(graph_img, (padding, graph_height), (graph_width + padding, graph_height), (255, 255, 255), 2)

    # y축 눈금 간격 계산 (최소 5개에서 최대 7개의 눈금이 표시되도록 설정)
    num_ticks = 7
    tick_interval = y_range / (num_ticks - 1)

    # 평균선 그리기
    average = statistics.mean(emotion_values1)
    y_pos = int(graph_height - (average - min_value) * y_scale)
    cv2.line(graph_img, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 150, 150), 2)
    cv2.putText(graph_img, "average", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 150, 150), 1)  # 레이블

    # 양의 기울기 평균 + 표준편차 기준선 그리기
    if len(emotion_values1) >= 10:
        avg_pos = statistics.mean(emotion_values1)
        std_pos = statistics.stdev(emotion_values1)

        y_pos = int(graph_height - (avg_pos + 2 * std_pos - min_value) * y_scale)  # 2시그마
        cv2.line(graph_img, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 120), 1)
        cv2.putText(graph_img, "+1sigma", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 140),
                    1)  # 레이블

        y_pos = int(graph_height - (avg_pos + 3 * std_pos - min_value) * y_scale)  # 3시그마
        cv2.line(graph_img, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 200), 1)
        cv2.putText(graph_img, "+2sigma", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 200),
                    1)  # 레이블

    # y축 눈금 및 값 표시
    for j in range(num_ticks):
        y_value = min_value + j * tick_interval
        y_pos = int(graph_height - (y_value - min_value) * y_scale)
        y_pos = max(10, min(y_pos, graph_height - 10))  # y_pos 값을 그래프 범위 내로 제한
        cv2.line(graph_img, (padding - 5, y_pos), (padding, y_pos), (255, 255, 255), 1)
        cv2.putText(graph_img, f'{y_value:.3f}', (2, y_pos + 3), cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale,
                    (255, 255, 255), 1)

    # 그래프 선 그리기
    for i in range(1, len(emotion_values1)):
        y1 = int(graph_height - (emotion_values1[i - 1] - min_value) * y_scale)
        y2 = int(graph_height - (emotion_values1[i] - min_value) * y_scale)
        x1 = padding + int((i - 1) * (graph_width / max_values_to_show))
        x2 = padding + int(i * (graph_width / max_values_to_show))
        cv2.line(graph_img, (x1, y1), (x2, y2), (0, 255, 0), 1)

    cv2.putText(graph_img, current_time, (graph_width - 80, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)  # 현재시간 표시

    cv2.putText(graph_img, f'{emotion_value:.3f}', (graph_width - 50, graph_height - 20),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (100, 100, 100), 2)  # z-score 표시

    # 시간 정보 표시
    for i, timestamp in enumerate(timestamps1):
        if i % 60 == 0:  # 뛰엄뛰엄 x축 표시
            # x 위치 계산
            x_pos = padding + int(i * (graph_width / max_values_to_show))

            # 텍스트 크기 및 위치 조정
            text_size = cv2.getTextSize(timestamp, cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, 1)[0]

            # 텍스트 그리기
            cv2.putText(graph_img, timestamp, (x_pos - text_size[0] // 2, graph_height + text_size[1] + 10),
                        cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, (255, 255, 255), 1)

            # y축 시간선 그리기
            cv2.line(graph_img, (x_pos - text_size[0] // 2 + int(padding / 2), 0),
                     (x_pos - text_size[0] // 2 + int(padding / 2), graph_height + text_size[1] + 10), (50, 50, 50), 1)

    # 그래프 표시
    cv2.imshow('Z-score GSR graph (Skin conductance)', graph_img)
    if not window_moved:
        cv2.moveWindow('Z-score GSR graph (Skin conductance)', init_positonx, init_positony)


# 필요한 전역 변수 초기화
emotion_values2 = []
slopes = []  # 기울기 값을 저장할 리스트
timestamps2 = []
padding = 50
graph_img2 = np.zeros((graph_height + 50, graph_width + padding, 3), dtype=np.uint8)
num = 0
pos_slopes = []


################ SCR 그래프 ################## (기본으로는 사용안함)
### SCR은 단위시간당 GSR의 변화량으로 간주함
def update_graph_SCR(emotion_value):  # SCR 그래프
    global emotion_values2, slopes, timestamps2, graph_img2

    current_time = time.strftime('%H:%M:%S')  # 현재 시간

    # 새 감정 값과 타임스탬프 추가
    emotion_values2.append(emotion_value)
    timestamps2.append(current_time)

    # 리스트의 길이가 max_values_to_show를 초과하면 가장 오래된 값을 제거
    if len(emotion_values2) > max_values_to_show:
        emotion_values2.pop(0)
        timestamps2.pop(0)
        slopes.pop(0)

    if len(pos_slopes) > max_values_to_show * 3:  # 3분 정도된 데이터는 제거 (속도 문제)
        pos_slopes.pop(0)

    # 현재 최대값과 최소값을 기준으로 y_scale 조정
    global max_slope, min_slope, last_calculated_index

    # 기울기 계산
    if len(emotion_values2) >= 2:
        slope = emotion_values2[-1] - emotion_values2[-2]
        slopes.append(slope)
        if slope > 0:  # 양의 기울기만 저장해둠
            pos_slopes.append(slope)
    else:
        slopes.append(0)

    # max_slope와 min_slope를 업데이트
    max_slope = max(slopes) if slopes and max(slopes) > 0.002 else 0.002
    min_slope = min(slopes) if slopes and min(slopes) < -0.002 else -0.002
    y_range = max_slope - min_slope if max_slope != min_slope else 0.004
    y_scale = graph_height / y_range

    # 그래프 이미지 초기화
    graph_img2[:] = 0

    # y축 축선 그리기
    cv2.line(graph_img2, (padding, 0), (padding, graph_height), (255, 255, 255), 2)

    # x축 축선 그리기
    cv2.line(graph_img2, (padding, graph_height), (graph_width + padding, graph_height), (255, 255, 255), 2)

    # y축 눈금 간격 계산 (최소 5개에서 최대 7개의 눈금이 표시되도록 설정)
    num_ticks = 7
    tick_interval = y_range / (num_ticks - 1)

    # y축 눈금 및 값 표시
    for j in range(num_ticks):
        y_value = min_slope + j * tick_interval
        y_pos = int(graph_height - (y_value - min_slope) * y_scale)
        y_pos = max(10, min(y_pos, graph_height - 10))  # y_pos 값을 그래프 범위 내로 제한
        cv2.line(graph_img2, (padding - 5, y_pos), (padding, y_pos), (255, 255, 255), 1)
        cv2.putText(graph_img2, f'{y_value:.3f}', (5, y_pos + 3), cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale,
                    (255, 255, 255), 1)

    # zero 기준선 그리기
    y_pos = int(graph_height - (0 - min_slope) * y_scale)
    cv2.line(graph_img2, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 150, 150), 1)
    cv2.putText(graph_img2, "zero", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (0, 150, 150), 1)  # 레이블

    # 양의 기울기 평균 + 표준편차 기준선 그리기
    if len(pos_slopes) >= 10:
        avg_pos = statistics.mean(pos_slopes)
        std_pos = statistics.stdev(pos_slopes)
        y_pos = int(graph_height - (avg_pos + std_pos - min_slope) * y_scale)  # 1시그마
        cv2.line(graph_img2, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 80), 1)
        cv2.putText(graph_img2, "+1sigma", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (0, 0, 140),
                    1)  # 레이블

        y_pos = int(graph_height - (avg_pos + 3 * std_pos - min_slope) * y_scale)  # 3시그마
        cv2.line(graph_img2, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 200), 1)
        cv2.putText(graph_img2, "+3sigma", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (0, 0, 200),
                    1)  # 레이블

    # 기울기 그래프 그리기
    for i in range(1, len(slopes)):
        y1 = int(graph_height - (slopes[i - 1] - min_slope) * y_scale)
        y2 = int(graph_height - (slopes[i] - min_slope) * y_scale)
        x1 = padding + int((i - 1) * (graph_width / max_values_to_show))
        x2 = padding + int(i * (graph_width / max_values_to_show))
        cv2.line(graph_img2, (x1, y1), (x2, y2), (0, 255, 0), 1)

    # 시간 정보 표시
    for i, timestamp in enumerate(timestamps2):
        if i % 60 == 0:  # 뛰엄뛰엄 x축 표시
            # x 위치 계산
            x_pos = padding + int(i * (graph_width / max_values_to_show))

            # 텍스트 크기 및 위치 조정
            text_size = cv2.getTextSize(timestamp, cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, 1)[0]

            # 텍스트 그리기
            cv2.putText(graph_img2, timestamp, (x_pos - text_size[0] // 2, graph_height + text_size[1] + 10),
                        cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, (255, 255, 255), 1)

    # 그래프 표시
    cv2.imshow('SCR graph (Phasic data)', graph_img2)
    global window_moved
    if not window_moved:
        cv2.moveWindow('SCR graph (Phasic data)', init_positonx, init_positony + graph_height + padding + 20)
        window_moved = True


# 필요한 전역 변수 초기화
emotion_values3 = []
timestamps3 = []
slopes3 = []  # 기울기 값을 저장할 리스트
total_slopes3 = []  # z-score를 위해 모든 slopes3 값을 계속 저장할 리스트
graph_img3 = np.zeros((graph_height + 50, graph_width + padding, 3), dtype=np.uint8)
SCLmax = 0
SCLmin = 99999
firstFlag = True  # 첫 GSR/SCR 쓰레기 데이터 버릴 플래그
SCLavg=SCLstd=0

################ SCR Z-score 그래프 ##################
# Range-corrected score에 max 값이 noise가 들어가면 값이 너무 왜곡 되어서 SCR z-score로 변경함.
def update_graph_SCR_zscore(emotion_value):
    global emotion_values3, slopes3, total_slopes3, timestamps3, graph_img3, firstFlag

    current_time = time.strftime('%H:%M:%S')  # 현재 시간

    # 새 감정 값과 타임스탬프 추가
    emotion_values3.append(emotion_value)
    timestamps3.append(current_time)

    # 리스트의 길이가 max_values_to_show를 초과하면 가장 오래된 값을 제거
    if len(emotion_values3) > max_values_to_show:
        emotion_values3.pop(0)
        timestamps3.pop(0)
        slopes3.pop(0)

    # 현재 최대값과 최소값을 기준으로 y_scale 조정
    global max_slope, min_slope, last_calculated_index, SCLavg, SCLstd

    slope = 0
    # 기울기 계산
    if len(emotion_values3) >= 2:
        slope = emotion_values3[-1] - emotion_values3[-2]
        slopes3.append(slope)
        total_slopes3.append(slope)

        if len(total_slopes3) >= 300 and firstFlag:
            total_slopes3 = total_slopes3[30:]  # 1분 넘어갈 때, 앞에 30개는 버리기. 처음 시작할 때 치솟는 이상 데이터 삭제
            firstFlag = False

        SCLavg = np.mean(total_slopes3)  # 총 평균
        SCLstd = np.std(total_slopes3)  # 총 표준편차

    else:
        slopes3.append(0)
        total_slopes3.append(0)
        SCLavg = 0
        SCLstd = 1

    # y축 rescaling 위한 최대 및 최소값 계산
    max_slope = (max(slopes3) - SCLavg) / SCLstd if slopes3 and (max(slopes3) - SCLavg) / SCLstd > 1 else 1
    min_slope = (min(slopes3) - SCLavg) / SCLstd if slopes3 and (min(slopes3) - SCLavg) / SCLstd < -1 else -1
    y_range = max_slope - min_slope if max_slope != min_slope else 2
    y_scale = graph_height / y_range

    # 그래프 이미지 초기화
    graph_img3[:] = 0

    # y축 축선 그리기
    cv2.line(graph_img3, (padding, 0), (padding, graph_height), (255, 255, 255), 2)

    # x축 축선 그리기
    cv2.line(graph_img3, (padding, graph_height), (graph_width + padding, graph_height), (255, 255, 255), 2)

    # y축 눈금 간격 계산 (최소 5개에서 최대 7개의 눈금이 표시되도록 설정)
    num_ticks = 5
    tick_interval = y_range / (num_ticks - 1)

    # y축 눈금 및 값 표시
    for j in range(num_ticks):
        y_value = min_slope + j * tick_interval
        y_pos = int(graph_height - (y_value - min_slope) * y_scale)
        y_pos = max(10, min(y_pos, graph_height - 10))  # y_pos 값을 그래프 범위 내로 제한
        cv2.line(graph_img3, (padding - 5, y_pos), (padding, y_pos), (255, 255, 255), 1)
        cv2.putText(graph_img3, f'{y_value:.3f}', (2, y_pos + 3), cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale,
                    (255, 255, 255), 1)

    # 0 기준선 그리기
    y_pos = int(graph_height - (0 - min_slope) * y_scale)
    cv2.line(graph_img3, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 150, 150), 2)
    cv2.putText(graph_img3, "zero", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 150, 150), 1)  # 레이블

    # 1 기준선 그리기
    y_pos = int(graph_height - (1 - min_slope) * y_scale)
    cv2.line(graph_img3, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 80), 1)
    cv2.putText(graph_img3, "+1", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 150), 1)  # 레이블

    # 2 기준선 그리기
    y_pos = int(graph_height - (2 - min_slope) * y_scale)
    cv2.line(graph_img3, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 150), 1)
    cv2.putText(graph_img3, "+2", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 200), 1)  # 레이블

    # 3 기준선 그리기
    y_pos = int(graph_height - (3 - min_slope) * y_scale)
    cv2.line(graph_img3, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 250), 1)
    cv2.putText(graph_img3, "+3", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 250), 1)  # 레이블

    # -1 기준선 그리기
    y_pos = int(graph_height - (-1 - min_slope) * y_scale)
    cv2.line(graph_img3, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 80), 1)
    cv2.putText(graph_img3, "-1", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.4, (0, 0, 200), 1)  # 레이블

    # 기울기 그래프 그리기
    for i in range(1, len(slopes3)):
        if (SCLmax - SCLmin) != 0:
            # R_slope1 = (slopes3[i - 1] - SCLmin) / (SCLmax - SCLmin) #Range-corrected
            # R_slope2 = (slopes3[i] - SCLmin) / (SCLmax - SCLmin) #Range-corrected

            R_slope1 = (slopes3[i - 1] - SCLavg) / SCLstd  # z-score
            R_slope2 = (slopes3[i] - SCLavg) / SCLstd  # z-score

            y1 = int(graph_height - (R_slope1 - min_slope) * y_scale)
            y2 = int(graph_height - (R_slope2 - min_slope) * y_scale)
            x1 = padding + int((i - 1) * (graph_width / max_values_to_show))
            x2 = padding + int(i * (graph_width / max_values_to_show))
            cv2.line(graph_img3, (x1, y1), (x2, y2), (0, 255, 0), 1)

    cv2.putText(graph_img3, f'{slope:.5f}', (graph_width - 50, graph_height - 70),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (100, 100, 100), 2)  # z-score 표시

    # 시간 정보 표시
    for i, timestamp in enumerate(timestamps3):
        if i % 60 == 0:  # 뛰엄뛰엄 x축 표시
            # x 위치 계산
            x_pos = padding + int(i * (graph_width / max_values_to_show))

            # 텍스트 크기 및 위치 조정
            text_size = cv2.getTextSize(timestamp, cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, 1)[0]

            # 텍스트 그리기
            cv2.putText(graph_img3, timestamp, (x_pos - text_size[0] // 2, graph_height + text_size[1] + 10),
                        cv2.FONT_HERSHEY_SIMPLEX, axis_font_scale, (255, 255, 255), 1)

            # y축 시간선 그리기
            cv2.line(graph_img3, (x_pos - text_size[0] // 2 + int(padding / 2), 0),
                     (x_pos - text_size[0] // 2 + int(padding / 2), graph_height + text_size[1] + 10), (50, 50, 50), 1)

    # 그래프 표시
    cv2.imshow('Z-score SCR graph (Phasic)', graph_img3)
    global window_moved
    if not window_moved:
        cv2.moveWindow('Z-score SCR graph (Phasic)', init_positonx, init_positony + graph_height + padding + 20)
        # cv2.moveWindow('Range-corrected SCR graph (Phasic)', init_positonx, init_positony + graph_height * 2 + padding + 100)

        window_moved = True

    # z_slop2 = (slopes3[-1] - SCLavg) / SCLstd
    return slopes3[-1]


# Define REC button properties
button_position = (padding - 40, 420)  # Top-left corner of the button
button_size = (50, 50)  # Width and height of the button
button_color = (0, 0, 150)  # Color when the button is OFF
button_pressed_color = (0, 150, 0)  # Color when the button is ON

SCR = 0
trial_no = 0
total_gsr_sc = []

while True:
    ################# 쉬머 데이터 읽기 #################
    try:
        while numbytes < framesize:
            ddata += ser.read(framesize)
            numbytes = len(ddata)

        data = ddata[0:framesize]
        ddata = ddata[framesize:]
        numbytes = len(ddata)

        # read basic packet information
        (packettype) = struct.unpack('B', data[0:1])
        (timestamp0, timestamp1, timestamp2) = struct.unpack('BBB', data[1:4])

        # read packet payload
        (PPG_raw, GSR_raw) = struct.unpack('HH', data[4:framesize])

        # get current GSR range resistor value
        Range = ((GSR_raw >> 14) & 0xff)  # upper two bits
        if (Range == 0):
            Rf = 40.2  # kohm
        elif (Range == 1):
            Rf = 287.0  # kohm
        elif (Range == 2):
            Rf = 1000.0  # kohm
        elif (Range == 3):
            Rf = 3300.0  # kohm

        # convert GSR to kohm value
        gsr_to_volts = (GSR_raw & 0x3fff) * (3.0 / 4095.0)
        GSR_ohm = Rf / ((gsr_to_volts / 0.5) - 1.0)
        GSR_sc = (1 / GSR_ohm) * 1000  # skin conductance

        # convert PPG to milliVolt value
        PPG_mv = PPG_raw * (3000.0 / 4095.0)

        timestamp = timestamp0 + timestamp1 * 256 + timestamp2 * 65536

        # Optional: uncomment the print command here below to visulise the measurements
        # print("0x{:.0f}02x,\t{:.0f},\t{:.4f},\t{:.4f}".format(packettype[0], timestamp, GSR_ohm, PPG_mv))

        total_gsr_sc.append(GSR_sc) # GSR_sc 데이터를 리스트에 추가

        GSRavg = np.mean(total_gsr_sc) # GSR_sc 평균
        GSRstd = np.std(total_gsr_sc) # GSR_sc 표준편차

        # GSR_sc z-score 계산
        if GSRstd != 0:
            GSR_sc_z_score = (GSR_sc - GSRavg) / GSRstd
        else:
            GSR_sc_z_score = 0

        mysample = [timestamp, GSR_sc, GSR_ohm, PPG_mv]
        outlet.push_sample(mysample)

        trial_no += 1

        # GSR 그래프 그리기
        if trial_no >= 7:  # 센서 어느 정도 안정화된 후,
            update_graph_GSR(GSR_sc_z_score)  ###<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< GSR 그래프
            # update_graph_SCR(GSR_sc)  ###<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< SCR 그래프 / 전체뷰
            SCR = update_graph_SCR_zscore(GSR_sc)  ###<<<<<<<<<<<<<<<<<<<<<<<<<< SCR z-score 그래프

            SCR_z_score = (SCR - SCLavg) / SCLstd ### z-score 계산

            ############ main process (main.py)로 값 전달 ###########
            print(f"{timestamp},{GSR_sc},{SCR},{PPG_mv},{SCR_z_score},{GSR_sc_z_score}")

    except KeyboardInterrupt:
        # send stop streaming command
        ser.write(struct.pack('B', 0x20))
        print("stop command sent, waiting for ACK_COMMAND")
        wait_for_ack()
        print("ACK_COMMAND received.")
        # close serial port
        ser.close()
        print("All done")

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cv2.destroyAllWindows()
