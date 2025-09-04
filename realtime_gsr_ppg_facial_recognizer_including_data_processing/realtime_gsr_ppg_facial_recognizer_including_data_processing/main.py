# ***********************************************************************
# 이 프로그램은 Shimmer GSR+를 이용한 GSR, SCR, PPG 신호를 기록하고,
# 딥러닝 모델 기반으로 Webcam으로 표정 인식한 결과와 눈동자 움직임 및 눈을 뜬 비율
# 데이터를 시각화하여 보여주고 하나의 엑셀 파일로 데이터를 기록해주는 프로그램입니다.
# main.py를 실행시키면, GSRoutput 폴더에 결과 csv파일이 저장됩니다.
#
#        *컴퓨터 사양에 따라 초기 실행에 다소 시간이 걸릴 수도 있습니다*
#                                                       @Huhn Kim, 2024
# ***********************************************************************
import math
import time, datetime
from keras.models import load_model
from keras.preprocessing.image import img_to_array
import csv
import cv2
import numpy as np
import subprocess  # 병렬 처리
import dlib  # 얼굴 탐지 라이브러리 (이 라이브러리 설치는 CMake와 Visual Studio Build Tools이 먼저 설치되어야 함. 인터넷 검색해보면 나옴)




# dlib의 얼굴 탐지기와 랜드마크 예측기 로드
detector = dlib.get_frontal_face_detector()
predictor = dlib.shape_predictor(r'D:/realtime_gsr_ppg_facial_recognizer_including_data_processing/realtime_gsr_ppg_facial_recognizer_including_data_processing/Model/shape_predictor_68_face_landmarks.dat')  # 68 랜드마크 모델 파일
classifier = load_model(r'D:/realtime_gsr_ppg_facial_recognizer_including_data_processing/realtime_gsr_ppg_facial_recognizer_including_data_processing/Model/model.h5')  ########## VGG 16 CNN model, 정확도 0.88 ############

emotion_labels = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']

cap = cv2.VideoCapture(0)  # 연결된 카메라가 여러 개일 때는 원하는 카메라 번호로 변경해야 함.
startdate = str(datetime.datetime.now().date()) + "_" + str(datetime.datetime.now().hour) + "h_" + str(
    datetime.datetime.now().minute) + "m"
date_file_name = "D:/realtime_gsr_ppg_facial_recognizer_including_data_processing/realtime_gsr_ppg_facial_recognizer_including_data_processing/GSRoutput/GSR_data_" + startdate + ".csv"  # <<<<<< 요기에 데이터 저장

# Define button properties
padding = 50
button_position = (padding - 40, 420)  # Top-left corner of the button
button_size = (50, 50)  # Width and height of the button
button_color = (0, 0, 150)  # Color when the button is OFF
button_pressed_color = (0, 150, 0)  # Color when the button is ON

EYE_AR_THRESH = 0.23  # 눈 감김 여부 판단을 위한 임계값 (일반적으로 0.2 ~ 0.25 사이)
values = 0  # GSR 쪽에서 데이터 4개로 잘 넘어오는지 확인하기 위한 변수

REC_flag = True  # 데이터를 엑셀로 출력할 지 말지 결정하는 변수

##############유니티 통신위한 추가 코드 ################################
# import socket
# HOST = '127.0.0.1'
# PORT = 8000
#
# server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
# server_socket.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
# server_socket.bind((HOST, PORT))
# server_socket.listen()
# client_socket, addr = server_socket.accept()
#
# print('Connected by', addr)
####################################################################

def toggle_variable(event, x, y, flags, param):
    global REC_flag
    if event == cv2.EVENT_LBUTTONDOWN:
        if button_position[0] <= x <= button_position[0] + button_size[0] and \
                button_position[1] <= y <= button_position[1] + button_size[1]:
            REC_flag = not REC_flag
            print(f'Button clicked! Variable REC_flag is now: {REC_flag}')


cv2.namedWindow('Emotion Detector')
cv2.setMouseCallback('Emotion Detector', toggle_variable)


def draw_text_with_border(image, text, position, font, font_scale, text_color, border_color, thickness):
    # Draw the border
    cv2.putText(image, text, position, font, font_scale, border_color, thickness + 2, lineType=cv2.LINE_AA)
    # Draw the text
    cv2.putText(image, text, position, font, font_scale, text_color, thickness, lineType=cv2.LINE_AA)


def draw_transparent_bar_graph(image, data, labels, position, size, color, alpha=0.5):  # 감정 그래프 그리기
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


def eye_aspect_ratio(eye):  # 눈이 감겼는지 여부 판단
    # 눈의 두 개 수직 거리 계산
    A = np.linalg.norm(np.array(eye[1]) - np.array(eye[5]))
    B = np.linalg.norm(np.array(eye[2]) - np.array(eye[4]))

    # 눈의 수평 거리 계산
    C = np.linalg.norm(np.array(eye[0]) - np.array(eye[3]))

    # EAR 계산
    ear = (A + B) / (2.0 * C)
    return ear


def unix_to_regular_time_with_ms(unix_timestamp):  # milisecond 단위로 시간 저장하기 위한 함수
    # UNIX 타임스탬프를 datetime 객체로 변환
    unix_timestamp = unix_timestamp / 1000.0
    dt_object = datetime.datetime.fromtimestamp(unix_timestamp)

    # datetime 객체를 'YYYY-MM-DD HH:MM:SS.mmm' 형식으로 변환 (밀리초 포함)
    regular_time_with_ms = dt_object.strftime('%Y-%m-%d %H:%M:%S.%f')[:-3]  # 마지막 세 자리를 잘라서 밀리초를 3자리로 유지

    return regular_time_with_ms


with open(date_file_name, 'w', newline='') as gsr_csvfile:
    # Writing to CSV file
    csv_writer = csv.writer(gsr_csvfile)

    # 첫 행 데이터 타이틀 출력
    row_data = ["timestamp", "date", "time", "time(ms)", "GSR", "GSR_z", "SCR", "SCR_z", "PPG", "Angry", "Disgust", "Fear", "Happy",
                "Neutral", "Sad", "Surprise", "Emotion", "Leye_X", "Leye_Y", "Reye_X", "Reye_Y", "Leye_open", "Reye_open",
                "Left_eye_h", "Right_eye_h", "Left_brow_h1", "Right_brow_h1", "Left_brow_h2", "Right_brow_h2", "Between_brows",
                "Left_chin_length", "Right_chin_length", "Mouth_width", "Mouth_inner_height", "Mouth_outer_height"]

    csv_writer.writerow(row_data)

    process = subprocess.Popen(
        ["python","gsr_recording.py"],  # Use "python" instead of "python3" if necessary
        stdout=subprocess.PIPE,  # Redirect stdout to capture the output
        stderr=subprocess.PIPE,  # Optionally capture stderr
        text=True,  # Ensure the output is captured as a string


    )  # 이렇게 병렬처리하지 않으면, GSR 그래프 그리는 속도가 갈수록 느려짐




    while True:

        line = process.stdout.readline()  # gsr_recording.py에서 데이터 받아오기







        gsrSignal = False  # GSR 신호 들어오는지 체크
        if line:
            values = line.strip().split(',')  # 넘어오는 결과값을 분할
            if len(values) == 6:
                timestamp, GSR_sc, SCR, PPG_mv, SCR_z, GSR_z = map(float, values)  # print(f"{GSR_sc},{SCR},{PPG_mv}")

                #############유니티 통신###############
                # msg = str(SCR_z)
                # client_socket.sendall(msg.encode())
                # print('send 완료 ' + str(SCR_z))
                ######################################

                gsrSignal = True # gsr 신호 들어왔음 flag
        else:
            print("GSR 생체신호 수신 대기중...")


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
        faces = detector(gray)

        now = datetime.datetime.now()
        current_time_ms = int(now.timestamp() * 1000)
        current_time = time.strftime('%H:%M:%S')

        cv2.putText(frame, current_time, (500, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)  # 현재시간 표시

        # 예측값 초기화
        prediction = [None, None, None, None, None, None, None]
        emotion_label = None
        left_eye_center = [None, None]
        right_eye_center = [None, None]
        left_eye_open = None
        right_eye_open = None

        # 얼굴을 찾지 못했을 때 사용할 기본값 설정
        left_eye_h = 0
        right_eye_h = 0
        left_brow_h1 = 0
        right_brow_h1 = 0
        left_brow_h2 = 0
        right_brow_h2 = 0
        between_brows = 0
        left_chin_length = 0
        right_chin_length = 0
        mouth_width = 0
        mouth_inner_height = 0
        mouth_outer_height = 0

        for face in faces:  # 인식된 모든 얼굴들에 대해,
            landmarks = predictor(gray, face)  # 얼굴의 랜드마크 예측

            # 얼굴 영역 추출 및 전처리
            x, y, w, h = face.left(), face.top(), face.width(), face.height()

            # roi_gray 영역을 추출할 때 유효한 영역인지 확인
            if w > 0 and h > 0:
                cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 255), thickness=4)
                roi_gray = gray[y:y + h, x:x + w]

                #roi_gray가 비어 있지 않은지 확인 후 리사이즈
                if roi_gray.size > 0:
                    roi_gray = cv2.resize(roi_gray, (48, 48), interpolation=cv2.INTER_AREA)
                else:
                    print("Warning: roi_gray is empty!")

                if np.sum([roi_gray]) != 0:
                    roi = roi_gray.astype('float') / 255.0
                    roi = img_to_array(roi)
                    # print(roi.shape) #(48,48,1) 데이터 형태
                    roi = np.expand_dims(roi, axis=0)

                    prediction = classifier.predict(roi)[0]  # 딥러닝 모델에 넣어서 표정별 예측비율 계산
                    print(prediction)
                    emotion_label = emotion_labels[prediction.argmax()]
                    label_position = (x, y)
                    cv2.putText(frame, emotion_label, label_position, cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

                    ############## 얼굴 랜드마크 좌표 추출 (0~ 67) ##############
                    fmark = [(landmarks.part(n).x, landmarks.part(n).y) for n in range(0, 68)]

                    left_eye_h = math.dist(fmark[39-1], fmark[41-1])
                    right_eye_h = math.dist(fmark[44-1], fmark[48-1])

                    left_brow_h1 = math.dist(fmark[18-1], fmark[37-1])
                    right_brow_h1 = math.dist(fmark[27-1], fmark[46-1])
                    left_brow_h2 = math.dist(fmark[22-1], fmark[40-1])
                    right_brow_h2 = math.dist(fmark[23-1], fmark[43-1])

                    between_brows = math.dist(fmark[22-1], fmark[23-1])

                    left_chin_length = math.dist(fmark[4-1], fmark[49-1])
                    right_chin_length = math.dist(fmark[14-1], fmark[55-1])

                    mouth_width = math.dist(fmark[49-1], fmark[55-1])
                    mouth_inner_height = math.dist(fmark[63 - 1], fmark[67 - 1])
                    mouth_outer_height = math.dist(fmark[52 - 1], fmark[58 - 1])
                    ##############


                    # 눈 좌표 추출 (좌: 36~41, 우: 42~47 랜드마크 포인트)
                    left_eye = [(landmarks.part(n).x, landmarks.part(n).y) for n in range(36, 42)]
                    right_eye = [(landmarks.part(n).x, landmarks.part(n).y) for n in range(42, 48)]


                    # 눈 주변에 원 그리기
                    for (lx, ly) in left_eye:
                        cv2.circle(frame, (lx, ly), 2, (255, 0, 0), -1)
                    for (rx, ry) in right_eye:
                        cv2.circle(frame, (rx, ry), 2, (255, 0, 0), -1)

                    # 눈 감김 여부 판단 (EAR 계산)
                    left_EAR = eye_aspect_ratio(left_eye)
                    left_eye_center = np.mean(left_eye, axis=0).astype(int)

                    right_EAR = eye_aspect_ratio(right_eye)
                    right_eye_center = np.mean(right_eye, axis=0).astype(int)

                    left_eye_open = right_eye_open = 0 #눈을 감음

                    # 눈이 감긴 경우 (EAR < 임계값)
                    if left_EAR > EYE_AR_THRESH:
                        # 왼쪽 눈이 감기지 않은 경우에만 원을 그립니다.
                        cv2.circle(frame, tuple(left_eye_center), 5, (0, 255, 0), 1)
                        left_eye_open = 1
                    if right_EAR > EYE_AR_THRESH:
                        # 오른쪽 눈이 감기지 않은 경우에만 원을 그립니다.
                        cv2.circle(frame, tuple(right_eye_center), 5, (0, 255, 0), 1)
                        right_eye_open = 1

                    # Dummy data for bar graphs
                    data1 = [prediction[0] * 100, prediction[1] * 100, prediction[2] * 100, prediction[3] * 100,
                              prediction[4] * 100, prediction[5] * 100, prediction[6] * 100]
                    labels1 = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']

                    # 감정 그래프 그리기
                    draw_transparent_bar_graph(frame, data1, labels1, position=(60, 10), size=(20, 150),
                                                color=(0, 255, 0))

                else:
                    cv2.putText(frame, 'No Faces', (40, 80), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

        if gsrSignal:  # 얼굴 표정인식은 안되더라도 GSR/PPG 데이터는 저장되도록 함
            # 엑셀에 아래 데이터 저장하기
            # timestamp, date, time, time(ms), GSR, GSR_z, SCR, SCR_z, PPG, Angry, Disgust, Fear, Happy, Neutral, Sad, Surprise, Emotion, Leye_X, Leye_Y, Reye_X, Reye_Y, Leye_open, Reye_open

            row_data = [timestamp, str(datetime.datetime.now().date()), current_time, current_time_ms, GSR_sc, GSR_z, SCR, SCR_z,
                        PPG_mv, prediction[0], prediction[1], prediction[2],
                        prediction[3], prediction[4], prediction[5], prediction[6], emotion_label, left_eye_center[0],
                        left_eye_center[1], right_eye_center[0], right_eye_center[1], left_eye_open, right_eye_open,
                        left_eye_h, right_eye_h, left_brow_h1, right_brow_h1, left_brow_h2, right_brow_h2, between_brows,
                        left_chin_length, right_chin_length, mouth_width, mouth_inner_height, mouth_outer_height]

            if REC_flag:  # 기록 버튼이 On 상태이면,
                csv_writer.writerow(row_data)  # 데이터 엑셀로 기록해두기

        cv2.imshow('Emotion Detector', frame)  # 이미지 반복 그리기

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
cv2.destroyAllWindows()
