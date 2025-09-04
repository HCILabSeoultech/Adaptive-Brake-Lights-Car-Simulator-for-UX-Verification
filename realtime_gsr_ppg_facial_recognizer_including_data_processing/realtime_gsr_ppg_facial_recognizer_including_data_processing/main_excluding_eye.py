# ***********************************************************************
# main.py에서 눈 움직임은 뺀 프로그램
#                                                       @Huhn Kim, 2024
# ***********************************************************************
import time

import tensorflow
from tensorflow import keras
from keras.models import load_model
from time import sleep
from keras.preprocessing.image import img_to_array
from keras.preprocessing import image
import os, csv
import cv2
import numpy as np
from datetime import datetime
import subprocess

# 얼굴 및 눈 인식용 Haar Cascade 로드
face_classifier = cv2.CascadeClassifier('Model/haarcascade_frontalface_default.xml')

classifier = load_model('model.h5')
########## VGG 16 CNN model, 정확도 0.88 ############

emotion_labels = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']

cap = cv2.VideoCapture(0)
startdate = str(datetime.now().date()) + "_" + str(datetime.now().hour) + "h_" + str(datetime.now().minute) + "m"
date_file_name = "GSRoutput/GSR_data_" + startdate + ".csv"  # <<<<<< 요기에 데이터 저장

padding = 50

# Define button properties
button_position = (padding - 40, 420)  # Top-left corner of the button
button_size = (50, 50)  # Width and height of the button
button_color = (0, 0, 150)  # Color when the button is OFF
button_pressed_color = (0, 150, 0)  # Color when the button is ON

REC_flag = False

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

with open(date_file_name, 'w', newline='') as gsr_csvfile:
    # Writing to CSV file
    csv_writer = csv.writer(gsr_csvfile)

    # 첫 행 데이터 타이틀 출력
    row_data = ["timestamp", "date", "time", "GSR", "SCR", "PPG", "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad",
                "Surprise", "Emotion"]
    csv_writer.writerow(row_data)

    process = subprocess.Popen(
        ["python", "gsr_recording.py"],  # Use "python" instead of "python3" if necessary
        stdout=subprocess.PIPE,  # Redirect stdout to capture the output
        stderr=subprocess.PIPE,  # Optionally capture stderr
        text=True  # Ensure the output is captured as a string
    )

    while True:

        line = process.stdout.readline()

        # Process the line
        if line:
            values = line.strip().split(',')
            if len(values) == 4:
                timestamp, GSR_sc, SCR, PPG_mv = map(float, values) #print(f"{GSR_sc},{SCR},{PPG_mv}")
        else:
            print("GSR 생체신호 대기중...")

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

        # now = datetime.now()
        # current_time = now.strftime('%I:%M:%S %p') #+ f".{now.microsecond // 1000:03d}"

        current_time = time.strftime('%H:%M:%S')
        cv2.putText(frame, current_time, (500, 30), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (255, 255, 255), 2)  # 현재시간 표시

        for (x, y, w, h) in faces:
            cv2.rectangle(frame, (x, y), (x + w, y + h), (0, 255, 255), thickness=5)

            roi_gray = gray[y:y + h, x:x + w]
            roi_gray = cv2.resize(roi_gray, (48, 48), interpolation=cv2.INTER_AREA)

            if np.sum([roi_gray]) != 0:
                roi = roi_gray.astype('float') / 255.0
                roi = img_to_array(roi)
                # print(roi.shape) #(48,48,1) 데이터 형태
                roi = np.expand_dims(roi, axis=0)

                prediction = classifier.predict(roi)[0]
                print(prediction)
                label = emotion_labels[prediction.argmax()]
                label_position = (x, y)
                cv2.putText(frame, label, label_position, cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)

                # Dummy data for bar graphs
                data1 = [prediction[0]*100, prediction[1]*100, prediction[2]*100, prediction[3]*100, prediction[4]*100, prediction[5]*100, prediction[6]*100]
                labels1 = ['Angry', 'Disgust', 'Fear', 'Happy', 'Neutral', 'Sad', 'Surprise']

                # 감정 그래프 그리기
                draw_transparent_bar_graph(frame, data1, labels1, position=(60, 10), size=(20, 150), color=(0, 255, 0))

                if line:
                    # 엑셀에 데이터 저장하기 ["timestamp", "GSR", "SCR", "PPG", "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad", "Surprise"]
                    row_data = [timestamp, str(datetime.now().date()), current_time, GSR_sc, SCR, PPG_mv, prediction[0], prediction[1], prediction[2],
                                prediction[3], prediction[4], prediction[5], prediction[6], label]

                    # Writing the row to the CSV file
                    if REC_flag:
                        csv_writer.writerow(row_data)  # 데이터 엑셀로 기록해두기

            else:
                cv2.putText(frame, 'No Faces', (40, 80), cv2.FONT_HERSHEY_SIMPLEX, 1, (0, 255, 0), 2)
        cv2.imshow('Emotion Detector', frame)

        if cv2.waitKey(1) & 0xFF == ord('q'):
            break

cap.release()
cv2.destroyAllWindows()
