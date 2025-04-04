import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import os
# --- 1. 데이터 불러오기 ---
fileName = "02_노영환_merged.csv"
current_folder = os.path.dirname(__file__)
csv_path = os.path.join(current_folder, fileName)
column_names = [
    "수준", "브레이크 유형", "간격", "현재 시간", "충돌여부", "최소 안전 거리",
    "선두 차량 가속도", "실험 차량 가속도", "선두 차량 속도", "실험 차량 속도",
    "엑셀 세기", "브레이크 세기", "차량 간 거리", "timestamp", "date", "time", "time(ms)",
    "GSR", "GSR_z", "SCR", "SCR_z", "PPG",
    "Angry", "Disgust", "Fear", "Happy", "Neutral", "Sad", "Surprise", "Emotion",
    "Leye_X", "Leye_Y", "Reye_X", "Reye_Y", "Leye_open", "Reye_open",
    "Left_eye_h", "Right_eye_h", "Left_brow_h1", "Right_brow_h1", "Left_brow_h2", "Right_brow_h2",
    "Between_brows", "Left_chin_length", "Right_chin_length", "Mouth_width", "Mouth_inner_height", "Mouth_outer_height"
]
df = pd.read_csv(csv_path, header=None, names=column_names, encoding='utf-8')

# --- 2. 시간 파싱 및 정렬 ---
def parse_time(t_str):
    try:
        return datetime.strptime(t_str, "%H:%M:%S:%f")
    except:
        return None
df["parsed_time"] = df["현재 시간"].apply(parse_time)
df = df.dropna(subset=["parsed_time"]).sort_values(by="parsed_time").reset_index(drop=True)

# --- 3. 블록 분할 ---
time_threshold = timedelta(milliseconds=1000)
block_ids = [0]
current_block = 0
for i in range(1, len(df)):
    if (df.loc[i, "parsed_time"] - df.loc[i-1, "parsed_time"]) > time_threshold:
        current_block += 1
    block_ids.append(current_block)
df["block_id"] = block_ids

# --- 4. 전처리 ---
df["브레이크 세기"] = pd.to_numeric(df["브레이크 세기"], errors='coerce')
df["실험 차량 가속도"] = pd.to_numeric(df["실험 차량 가속도"], errors='coerce')
df["실험 차량 속도"] = pd.to_numeric(df["실험 차량 속도"], errors='coerce')
df["선두 차량 가속도"] = pd.to_numeric(df["선두 차량 가속도"], errors='coerce')
df["엑셀 세기"] = pd.to_numeric(df["엑셀 세기"], errors='coerce')

# --- 5. 통계 함수 정의 ---
def calc_block_features(block):
    block = block.sort_values("parsed_time")
    block_id = block["block_id"].iloc[0]
    meta = block.iloc[0][["브레이크 유형", "수준", "간격", "선두 차량 가속도"]].copy()
    meta["감속률"] = meta.pop("선두 차량 가속도")
    
    # 반응 시작: 실험 차량 속도가 100 이상에서 100 미만으로 처음 떨어지는 순간
    speed_series = block["실험 차량 속도"].fillna(999).values
    time_series = block["parsed_time"].values
    rt_start = None
    for i in range(1, len(speed_series)):
        if speed_series[i-1] >= 100 and speed_series[i] < 100:
            rt_start = time_series[i]
            break
    if rt_start is None:
        return None

    # 반응 종료: 브레이크 세기가 0.1 이하가 되는 시점 (엑셀 세기는 고려하지 않음)
    rt_end = block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)]
    rt_end_time = rt_end["parsed_time"].iloc[-1] if not rt_end.empty else block["parsed_time"].iloc[-1]

    # 이상치 클리핑: 실험 차량 가속도는 [-7.677078, 8.042717] 범위로 치환
    clipped_accel = block["실험 차량 가속도"].clip(lower=-7.677078, upper=8.042717)
    accel_during_reaction = clipped_accel[(block["parsed_time"] >= rt_start) & (block["parsed_time"] <= rt_end_time)]
    
    reaction_duration = (rt_end_time - rt_start).total_seconds() * 1000
    reaction_start_time = (rt_start - block["parsed_time"].iloc[0]).total_seconds() * 1000

    return pd.Series({
        "브레이크 유형": meta["브레이크 유형"],
        "수준": meta["수준"],
        "간격": meta["간격"],
        "감속률": meta["감속률"],
        "Accel_Min": accel_during_reaction.min(),
        "Accel_Max": accel_during_reaction.max(),
        "Accel_Mean": accel_during_reaction.mean(),
        "Reaction_Duration": reaction_duration,
        "Reaction_Start_Time": reaction_start_time,
        "block_id": block_id
    })

# --- 6. 통계 계산 ---
block_data = df.groupby("block_id").apply(calc_block_features).dropna().reset_index(drop=True)

# --- 7. 시도 번호 부여 ---
block_data = block_data.sort_values(by=["브레이크 유형", "수준", "간격", "감속률", "block_id"])
block_data["시도 번호"] = block_data.groupby(["브레이크 유형", "수준", "간격", "감속률"]).cumcount() + 1

# --- 8. 열 순서 정리 ---
sorted_columns = [
    "브레이크 유형", "수준", "간격", "감속률", "시도 번호",
    "Accel_Min", "Accel_Max", "Accel_Mean",
    "Reaction_Duration", "Reaction_Start_Time", "block_id"
]
final_df = block_data[sorted_columns].copy()

# --- 9. 정렬 순서 지정 ---
brake_order = ["기본제동등A", "밝기변화제동등B", "점멸주파수변화제동등C", "면적변화제동등D"]
final_df["브레이크 유형"] = pd.Categorical(final_df["브레이크 유형"], categories=brake_order, ordered=True)
final_df = final_df.sort_values(by=["브레이크 유형", "수준", "간격", "감속률", "시도 번호"],
                                ascending=[True, True, True, False, True]).reset_index(drop=True)

# --- 10. CSV 파일 저장 (UTF-8 with BOM) ---
output_fileName = fileName.replace("merged", "preprocessed")
output_path = os.path.join(current_folder, output_fileName)
final_df.to_csv(output_path, index=False, encoding='utf-8-sig')

# import ace_tools as tools

# tools.display_dataframe_to_user(name="시도 순서별 블록 요약 (최종)", dataframe=final_df)
