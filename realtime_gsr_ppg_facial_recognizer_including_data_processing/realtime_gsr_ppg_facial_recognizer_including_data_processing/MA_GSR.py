import os
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from PyEMD import EMD

# 디렉터리 정의
data_dir = "realtime_gsr_ppg_facial_recognizer_including_data_processing/GSR_ma_test/"
output_dir = "Processed_GSR/"
os.makedirs(output_dir, exist_ok=True)

import os
print("CWD =", os.getcwd(), "GSR_ma_test exists?", os.path.isdir("GSR_ma_test"))

# EMD를 사용하여 GSR 데이터 처리 및 시각화하는 함수
def process_gsr_with_emd(file_path):
    # CSV파일 불러오기rktk
    data = pd.read_csv(file_path, header=0)

    # CSV파일의 5번째 컬럼(GSR 데이터)만 추출하여 gsr_signal 변수에 저장
    try:
        gsr_signal = data.iloc[1:, 4].values
    except Exception as e:
        print(f"Error processing file {file_path}: {e}")
        return

    # 유효한 데이터가 없으면 경고 메세지 출력 후 종료
    if len(gsr_signal) == 0:
        print(f"File {file_path} contains no valid GSR data.")
        return

    # max_imf 매개변수를 사용하여 GSR 신호에 EMD를 적용
    emd = EMD()
    imfs = emd(gsr_signal, max_imf=3)  # 최대 4개의 IMF만 추출 (결과 확인 후 변경 필요시 개수 변경)
    residual = emd.residue

    # residual 데이터를 GSR 컬럼에 대체
    data.iloc[1:, 4] = residual  # GSR 데이터에 residual 데이터로 대체

    # residual z-score 계산 및 GSR_z 컬럼에 대체
    residual_mean = np.mean(residual)
    residual_std = np.std(residual)
    residual_zscore = (residual - residual_mean) / residual_std  # Z-score 계산
    data.iloc[1:, 5] = residual_zscore  # 6번째 컬럼에 저장


    # residual의 변화율을 계산하여 SCR 신호로 사용
    scr_signal = np.gradient(residual)

    # 7번째 컬럼에 SCR 데이터 추가
    data.iloc[1:, 6] = scr_signal  # SCR 데이터에 residual 변화율 추가

    # SCR z-score 계산 및 SCR_z 컬럼에 대체
    scr_mean = np.mean(scr_signal)
    scr_std = np.std(scr_signal)
    scr_zscore = (scr_signal - scr_mean) / scr_std  # Z-score 계산
    data.iloc[1:, 7] = scr_zscore  # 8번째 컬럼에 저장

    # 새로운 CSV 파일 저장
    new_file_path = os.path.join(output_dir, os.path.basename(file_path))
    data.to_csv(new_file_path, index=False, header=True)
    print(f"signal processed through emd : {new_file_path}")

    # # 원신호, imf, residual 신호를 그래프로 출력
    # num_imfs = len(imfs)
    # plt.figure(figsize=(20, 12))

    # plt.subplot(num_imfs + 2, 1, 1)
    # plt.plot(gsr_signal, label="Original GSR Signal")
    # plt.title("Original GSR Signal")
    # plt.legend()

    # for i, imf in enumerate(imfs):
    #     plt.subplot(num_imfs + 2, 1, i + 2)
    #     plt.plot(imf, label=f"IMF {i + 1}")
    #     plt.title(f"IMF {i + 1}")
    #     plt.legend()

    # plt.subplot(num_imfs + 2, 1, num_imfs + 2)
    # plt.plot(residual, label="Residual")
    # plt.title("Residual Signal")
    # plt.legend()

    # plt.tight_layout()
    # plt.show()


# data_dir에 있는 모든 csv 파일 처리
for file_name in os.listdir(data_dir):
    if file_name.endswith(".csv"):
        file_path = os.path.join(data_dir, file_name)
        print(f"Processing file: {file_name}")
        process_gsr_with_emd(file_path)
