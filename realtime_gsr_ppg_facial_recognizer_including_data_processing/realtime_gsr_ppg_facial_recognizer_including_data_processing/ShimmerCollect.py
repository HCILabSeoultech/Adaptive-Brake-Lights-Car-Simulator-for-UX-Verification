#!/usr/bin/python
###################################################
####  GSR(Skin resistance), PPG(mv) 데이터 실시간 획득
###################################################

import struct, serial
from pylsl import StreamInfo, StreamOutlet, resolve_stream, StreamInlet
import statistics
from keras.models import load_model
from keras.preprocessing.image import img_to_array
import os, csv
import cv2
import numpy as np
import time
from datetime import datetime

face_classifier = cv2.CascadeClassifier('Model/haarcascade_frontalface_default.xml')
classifier = load_model('model.h5')
########## VGG 16 CNN model, 정확도 0.88 ############

emotion_labels = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']
cap = cv2.VideoCapture(0)

##################################################
btPort = "COM4"  # 블루투스 포트 설정
##################################################

task = 1.0  # dummy task 태스크 번호 입력
startdate = str(datetime.now().date()) + "_" + str(datetime.now().hour) + "h_" + str(datetime.now().minute) + "m"
window_moved = False # 처음에만 윈도우 창 이동위한 플래그
init_positonx, init_positony = 100, 100 # 윈도우 창이 뜨는 초기 위치
sampling_freq = 10  # 50
max_values_to_show = sampling_freq * 60 * 1  # 1분 그래프에 표시할 최대 값의 개수

def wait_for_ack():
    ddata = ""
    ack = struct.pack('B', 0xff)
    while ddata != ack:
        ddata = ser.read(1)
        # print("0x{}02x".format(ord(ddata[0])))
        print('{}'.format(ddata[0]))
    return


def draw_text_with_border(image, text, position, font, font_scale, text_color, border_color, thickness):
    # Draw the border
    cv2.putText(image, text, position, font, font_scale, border_color, thickness + 2, lineType=cv2.LINE_AA)
    # Draw the text
    cv2.putText(image, text, position, font, font_scale, text_color, thickness, lineType=cv2.LINE_AA)


def draw_transparent_bar_graph(image, data, labels, position, size, color, alpha=0.5):
    x, y = position
    height, width = size
    max_value = max(data)

    overlay = image.copy()

    for i, (value, label) in enumerate(zip(data, labels)):
        bar_width = int((value / max_value) * width)
        # Draw the bar
        cv2.rectangle(overlay, (x, y + i * height), (x + bar_width, y + (i + 1) * height), color, -1)
        # Draw the label with a border
        label_position = (x - 50, y + i * height + height // 2 + 5)
        draw_text_with_border(overlay, label, label_position, cv2.FONT_HERSHEY_SIMPLEX, 0.6, (0, 255, 255), (0, 0, 0),
                              2)

    # Blend the overlay with the original image
    cv2.addWeighted(overlay, alpha, image, 1 - alpha, 0, image)


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
print("Packet Type\tTimestamp\tGSR\tPPG")
############################################################


# /////////////////////////////////////////////// GSR 그래프 그리기
# Parameters for the graph
graph_width = 1200  # 그래프 이미지의 너비
graph_height = 440  # 그래프 이미지의 높이
padding = 50  # y축 눈금을 위한 왼쪽 패딩
last_calculated_index = -sampling_freq  # 마지막 기울기 계산 인덱스 초기화

# Create an empty black image for the graph
graph_img = np.zeros((graph_height + padding, graph_width + padding, 3), dtype=np.uint8)

# 각 데이터 포인트에 대한 타임스탬프 저장
timestamps1 = []
emotion_values1 = []

def update_graph1(emotion_value):
    # 현재 시간 추가
    current_time = time.strftime('%H:%M:%S')

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
    cv2.line(graph_img, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 150, 150), 1)
    cv2.putText(graph_img, "average", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (0, 150, 150), 1)  # 레이블

    # y축 눈금 및 값 표시
    for j in range(num_ticks):
        y_value = min_value + j * tick_interval
        y_pos = int(graph_height - (y_value - min_value) * y_scale)
        y_pos = max(10, min(y_pos, graph_height - 10))  # y_pos 값을 그래프 범위 내로 제한
        cv2.line(graph_img, (padding - 5, y_pos), (padding, y_pos), (255, 255, 255), 1)
        cv2.putText(graph_img, f'{y_value:.3f}', (5, y_pos + 3), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (255, 255, 255), 1)

    # 그래프 선 그리기
    for i in range(1, len(emotion_values1)):
        y1 = int(graph_height - (emotion_values1[i - 1] - min_value) * y_scale)
        y2 = int(graph_height - (emotion_values1[i] - min_value) * y_scale)
        x1 = padding + int((i - 1) * (graph_width / max_values_to_show))
        x2 = padding + int(i * (graph_width / max_values_to_show))
        cv2.line(graph_img, (x1, y1), (x2, y2), (0, 255, 0), 1)

    cv2.putText(graph_img, current_time, (graph_width - 80, 30),
                cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)  # 현재시간 표시

    # 시간 정보 표시
    for i, timestamp in enumerate(timestamps1):
        if i % 60 == 0:  # 뛰엄뛰엄 x축 표시
            # x 위치 계산
            x_pos = padding + int(i * (graph_width / max_values_to_show))

            # 텍스트 크기 및 위치 조정
            font_scale = 0.3
            text_size = cv2.getTextSize(timestamp, cv2.FONT_HERSHEY_SIMPLEX, font_scale, 1)[0]

            # 텍스트 그리기
            cv2.putText(graph_img, timestamp, (x_pos - text_size[0] // 2, graph_height + text_size[1] + 5),
                        cv2.FONT_HERSHEY_SIMPLEX, font_scale, (255, 255, 255), 1)

    # 그래프 표시
    cv2.imshow('GSR graph (Skin conductance)', graph_img)
    if not window_moved:
        cv2.moveWindow('GSR graph (Skin conductance)', init_positonx, init_positony)


# 필요한 전역 변수 초기화
emotion_values2 = []
slopes = []  # 기울기 값을 저장할 리스트
timestamps2 = []
padding = 50
graph_img2 = np.zeros((graph_height + 50, graph_width + padding, 3), dtype=np.uint8)
num = 0
pos_slopes = []


def update_graph2(emotion_value):
    global emotion_values2, slopes, timestamps2, graph_img2

    # 현재 시간 추가
    current_time = time.strftime('%H:%M:%S')

    # 새 감정 값과 타임스탬프 추가
    emotion_values2.append(emotion_value)
    timestamps2.append(current_time)

    # 리스트의 길이가 max_values_to_show를 초과하면 가장 오래된 값을 제거
    if len(emotion_values2) > max_values_to_show:
        emotion_values2.pop(0)
        timestamps2.pop(0)
        slopes.pop(0)

    if len(pos_slopes) > max_values_to_show * 10: #10분 정도 오래된 데이터는 제거 (속도 문제)
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

    # 기울기 변화 감지
    # slope_change, last_calculated_index = calculate_slope_change(slopes, last_calculated_index)

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
        cv2.putText(graph_img2, f'{y_value:.3f}', (5, y_pos + 3), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (255, 255, 255), 1)

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

        y_pos = int(graph_height - (avg_pos + 2 * std_pos - min_slope) * y_scale)  # 2시그마
        cv2.line(graph_img2, (padding - 5, y_pos), (graph_width + padding, y_pos), (0, 0, 200), 1)
        cv2.putText(graph_img2, "+2sigma", (padding + 5, y_pos - 10), cv2.FONT_HERSHEY_SIMPLEX, 0.3, (0, 0, 200),
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
            font_scale = 0.3
            text_size = cv2.getTextSize(timestamp, cv2.FONT_HERSHEY_SIMPLEX, font_scale, 1)[0]

            # 텍스트 그리기
            cv2.putText(graph_img2, timestamp, (x_pos - text_size[0] // 2, graph_height + text_size[1] + 5),
                        cv2.FONT_HERSHEY_SIMPLEX, font_scale, (255, 255, 255), 1)

    # 그래프 표시
    cv2.imshow('SCR graph (Phasic data)', graph_img2)
    global window_moved
    if not window_moved:
        cv2.moveWindow('SCR graph (Phasic data)', init_positonx, init_positony + graph_height + padding + 20)
        window_moved = True

    return slopes[-1]

# Define button properties
button_position = (padding - 40, 420)  # Top-left corner of the button
button_size = (50, 50)  # Width and height of the button
button_color = (0, 0, 150)  # Color when the button is OFF
button_pressed_color = (0, 150, 0)  # Color when the button is ON


# Function to toggle the variable
def toggle_variable(event, x, y, flags, param):
    global REC_flag
    if event == cv2.EVENT_LBUTTONDOWN:
        if button_position[0] <= x <= button_position[0] + button_size[0] and \
                button_position[1] <= y <= button_position[1] + button_size[1]:
            REC_flag = not REC_flag
            print(f'Button clicked! Variable REC_flag is now: {REC_flag}')


# Create a window and set mouse callback
cv2.namedWindow('Emotion Detector')
cv2.setMouseCallback('Emotion Detector', toggle_variable)

# ///////////////////////////////////////////////////////////
REC_flag = True

trial_no = 0
SCR = 0
date_file_name = "GSRoutput/GSR_data_" + startdate + ".csv"  # <<<<<< 요기에 데이터 저장
with open(date_file_name, 'w', newline='') as gsr_csvfile:
    # Writing to CSV file
    csv_writer = csv.writer(gsr_csvfile)

    # 첫 행 데이터 타이틀 출력
    row_data = ["timestamp", "time", "GSR", "SCR", "PPG", "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad",
                "Surprise", "Emotion", "Leye_X", "Leye_Y", "Reye_X", "Reye_Y"]
    csv_writer.writerow(row_data)

    while True:
        #### 쉬머 데이터 읽기
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

            # Send data to LSL
            mysample = [timestamp, GSR_sc, GSR_ohm, PPG_mv]
            # print("timestamp, GSR_SC, GSR_R, PPG", mysample)
            outlet.push_sample(mysample)
            trial_no += 1

            # GSR 그래프 그리기
            if trial_no > 10 and REC_flag:  # 센서 안정화된 후,
                update_graph1(GSR_sc)  ###<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< 요기에 GSR 그래프
                SCR = update_graph2(GSR_sc)  ###<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<<< 요기에 SCR 그래프

        except KeyboardInterrupt:
            # send stop streaming command
            ser.write(struct.pack('B', 0x20))
            print("stop command sent, waiting for ACK_COMMAND")
            wait_for_ack()
            print("ACK_COMMAND received.")
            # close serial port
            ser.close()
            print("All done")

        # 얼굴 표정 인식
        ret, frame = cap.read()
        if not ret:
            continue

        # Draw the button
        cv2.putText(frame, 'REC', (button_position[0] + 10, button_position[1] - 15),
                    cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 2)
        if REC_flag:
            cv2.rectangle(frame, button_position,
                          (button_position[0] + button_size[0], button_position[1] + button_size[1]),
                          button_pressed_color, -1)
            cv2.putText(frame, 'ON', (button_position[0] + 13, button_position[1] + 35),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 0), 2)
        else:
            cv2.rectangle(frame, button_position,
                          (button_position[0] + button_size[0], button_position[1] + button_size[1]),
                          button_color, -1)
            cv2.putText(frame, 'OFF', (button_position[0] + 10, button_position[1] + 35),
                        cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 0), 2)

        labels = []
        gray = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
        faces = face_classifier.detectMultiScale(gray, 1.32, 5)

        current_time = time.strftime('%H:%M:%S')
        cv2.putText(frame, current_time, (500, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)  # 현재시간 표시

        for (x, y, w, h) in faces:
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 255), thickness=5)

            roi_gray = gray[y:y + h, x:x + w]
            roi_gray = cv2.resize(roi_gray, (48, 48), interpolation=cv2.INTER_AREA)

            if np.sum([roi_gray]) != 0:
                roi = roi_gray.astype('float') / 255.0
                roi = img_to_array(roi)
                roi = np.expand_dims(roi, axis=0)

                prediction = classifier.predict(roi)[0]
                # print("표정예측 결과: ", prediction)
                label = emotion_labels[prediction.argmax()]
                label_position = (x, y)
                cv2.putText(frame, label, label_position, cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

                # Dummy data for bar graphs
                data1 = [prediction[0] * 100, prediction[1] * 100, prediction[2] * 100, prediction[3] * 100,
                         prediction[4] * 100, prediction[5] * 100, prediction[6] * 100]
                labels1 = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']

                # 표정인식 감정 그래프 그리기
                draw_transparent_bar_graph(frame, data1, labels1, position=(60, 10), size=(20, 150), color=(0, 255, 0))

                # 엑셀에 데이터 저장하기 ["timestamp", "GSR", "SCR", "PPG", "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad", "Surprise"]
                row_data = [timestamp, current_time, GSR_sc, SCR, PPG_mv, prediction[0], prediction[1], prediction[2],
                            prediction[3], prediction[4], prediction[5], prediction[6], label]

                # Writing the row to the CSV file
                if REC_flag:
                    csv_writer.writerow(row_data)  # 데이터 엑셀로 기록해두기

            else:
                cv2.putText(frame, 'No Faces', (40, 80), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

        cv2.imshow('Emotion Detector', frame)
        if not window_moved:
            cv2.moveWindow('Emotion Detector', init_positonx + graph_width + padding, init_positony)

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
cv2.destroyAllWindows()
