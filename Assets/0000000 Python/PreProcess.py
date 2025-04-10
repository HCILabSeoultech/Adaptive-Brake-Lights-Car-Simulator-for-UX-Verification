import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import os

# --- 1. 데이터 불러오기 ---
fileName = "05_이주현_merged.csv"  # 사용할 파일명 (예시)
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

# --- 4. 전처리 (자료형 변환) ---
df["브레이크 세기"] = pd.to_numeric(df["브레이크 세기"], errors='coerce')
df["실험 차량 가속도"] = pd.to_numeric(df["실험 차량 가속도"], errors='coerce')
df["실험 차량 속도"] = pd.to_numeric(df["실험 차량 속도"], errors='coerce')
df["선두 차량 가속도"] = pd.to_numeric(df["선두 차량 가속도"], errors='coerce')
df["엑셀 세기"] = pd.to_numeric(df["엑셀 세기"], errors='coerce')

# --- 5. 통계 함수 정의 ---
def calc_block_features(block):
    block = block.sort_values("parsed_time")
    block_id = block["block_id"].iloc[0]
    meta = block.iloc[0][["브레이크 유형", "수준", "간격", "선두 차량 가속도", "충돌여부"]].copy()
    meta["감속률"] = meta.pop("선두 차량 가속도")
    
    # 집계: 블록 내 충돌여부 => True가 한 번이라도 있으면 True, 아니면 False
    collision = block["충돌여부"].astype(str).str.upper().eq("TRUE").any()
    # 집계: 최소 안전 거리 => 모든 행이 True여야 True, 한 번이라도 False면 False
    safe_distance = block["최소 안전 거리"].astype(str).str.upper().eq("TRUE").all()
    
    # 반응 시작: 실험 차량 속도가 100 이상에서 처음 100 미만이 되는 시점
    speed_series = block["실험 차량 속도"].fillna(999).values
    time_series = block["parsed_time"].values
    rt_start = None
    for i in range(1, len(speed_series)):
        if speed_series[i-1] >= 100 and speed_series[i] < 100:
            rt_start = time_series[i]
            break
    if rt_start is None:
        rt_start = block["parsed_time"].iloc[0]
        reaction_duration = 0
        reaction_start_time = 0
    else:
        rt_end_rows = block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)]
        rt_end_time = rt_end_rows["parsed_time"].iloc[-1] if not rt_end_rows.empty else block["parsed_time"].iloc[-1]
        reaction_duration = (rt_end_time - rt_start).total_seconds() * 1000
        reaction_start_time = (rt_start - block["parsed_time"].iloc[0]).total_seconds() * 1000

    clipped_accel = block["실험 차량 가속도"].clip(lower=-7.677078, upper=8.042717)
    accel_during_reaction = clipped_accel[(block["parsed_time"] >= rt_start) & (block["parsed_time"] <= (block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)]["parsed_time"].iloc[-1] if not block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)].empty else block["parsed_time"].iloc[-1]))]

    if reaction_start_time == 0 and reaction_duration == 0:
        accel_mean = 0
        accel_min = 0
        accel_max = 0
    else:
        accel_mean = accel_during_reaction.mean()
        accel_min = accel_during_reaction.min()
        accel_max = accel_during_reaction.max()

    return pd.Series({
        "브레이크 유형": meta["브레이크 유형"],
        "수준": meta["수준"],
        "간격": meta["간격"],
        "선두 차량 감속률": meta["감속률"],
        "충돌여부": collision,
        "최소 안전 거리": safe_distance,  # 추가된 부분
        "반응 시간": reaction_start_time,
        "Reaction_Duration": reaction_duration,
        "실험 차량 평균 감속률": accel_mean,
        "실험 차량 최소 감속률": accel_min,
        "실험 차량 최대 감속률": accel_max,
        "block_id": block_id
    })

# --- 6. 통계 계산 ---
block_data = df.groupby("block_id", group_keys=False).apply(calc_block_features).reset_index(drop=True)

# --- 7. 시도 번호 부여 (각 조건 내 블록을 시간 순서로 나열) ---
block_data = block_data.sort_values(by=["브레이크 유형", "수준", "간격", "선두 차량 감속률", "block_id"])  # '감속률'을 '선두 차량 감속률'로 변경
block_data["시도 번호"] = block_data.groupby(["브레이크 유형", "수준", "간격", "선두 차량 감속률"]).cumcount() + 1

# --- 8. 열 순서 정리 및 새로운 열 추가 ---
# 브레이크 평균 세기 계산
def calc_brake_mean(block):
    if block["반응 시간"] == 0 and block["Reaction_Duration"] == 0:
        return 0
    else:
        return df[df["block_id"] == block["block_id"]]["브레이크 세기"].mean()

block_data["브레이크 평균 세기"] = block_data.apply(calc_brake_mean, axis=1)

# 열 이름 변경: "최소 안전 거리" -> "최소 안전 거리 유지"
block_data = block_data.rename(columns={"최소 안전 거리": "최소 안전 거리 유지"})

# 열 순서 정리
sorted_columns = [
    "브레이크 유형", "수준", "간격", "시도 번호", "선두 차량 감속률", "실험 차량 평균 감속률",
    "브레이크 평균 세기", "반응 시간", "Reaction_Duration", "충돌여부", "최소 안전 거리 유지", "block_id"
]
final_df = block_data[sorted_columns]

# --- 9. 정렬 순서 지정 ---
brake_order = ["기본제동등A", "밝기변화제동등B", "점멸주파수변화제동등C", "면적변화제동등D"]
final_df = final_df.copy()  # 명시적으로 복사
final_df["브레이크 유형"] = pd.Categorical(final_df["브레이크 유형"], categories=brake_order, ordered=True)
# "간격"은 오름차순, "감속률"은 내림차순, "시도 번호"는 오름차순
final_df = final_df.sort_values(
    by=["브레이크 유형", "수준", "간격", "선두 차량 감속률", "시도 번호"],
    ascending=[True, True, True, False, True]
).reset_index(drop=True)

# --- 10. CSV 파일 저장 (UTF-8 with BOM) ---
output_fileName = fileName.replace("merged", "preprocessed")
output_path = os.path.join(current_folder, output_fileName)
final_df.to_csv(output_path, index=False, encoding='utf-8-sig')
