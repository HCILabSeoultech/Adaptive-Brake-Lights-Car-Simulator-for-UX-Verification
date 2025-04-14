import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import os

# --- 1. 생체 데이터 피크 및 통계 계산 함수 ---
def extract_bio_features(series):
    valid_series = series.dropna()
    if valid_series.empty:
        return [np.nan] * 6
    diffs = np.diff(valid_series)
    peaks = np.sum((np.hstack([[0], diffs]) > 0) & (np.hstack([diffs, [0]]) < 0))
    return [
        valid_series.mean(),
        valid_series.std(),
        valid_series.max(),
        valid_series.min(),
        valid_series.max() - valid_series.min(),
        peaks
    ]

# --- 2. 전체 블록 통계 함수 (생체 포함) ---
def calc_block_features_with_bio(block):
    block = block.sort_values("parsed_time")
    block_id = block["block_id"].iloc[0]
    meta = block.iloc[0][["브레이크 유형", "수준", "간격", "선두 차량 가속도", "충돌여부"]].copy()
    meta["감속률"] = meta.pop("선두 차량 가속도")

    collision = block["충돌여부"].astype(str).str.upper().eq("TRUE").any()
    safe_distance = block["최소 안전 거리"].astype(str).str.upper().eq("TRUE").all()

    speed_series = block["실험 차량 속도"].fillna(999).values
    time_series = block["parsed_time"].values
    rt_start = None
    for i in range(1, len(speed_series)):
        if speed_series[i - 1] >= 100 and speed_series[i] < 100:
            rt_start = time_series[i]
            break
    if rt_start is None:
        rt_start = block["parsed_time"].iloc[0]
        reaction_duration = 0
        reaction_start_time = 2500
    else:
        rt_end_rows = block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)]
        rt_end_time = rt_end_rows["parsed_time"].iloc[-1] if not rt_end_rows.empty else block["parsed_time"].iloc[-1]
        reaction_duration = (rt_end_time - rt_start).total_seconds() * 1000
        reaction_start_time = (rt_start - block["parsed_time"].iloc[0]).total_seconds() * 1000

    clipped_accel = block["실험 차량 가속도"].clip(lower=-7.677078, upper=8.042717)
    accel_during_reaction = clipped_accel[
        (block["parsed_time"] >= rt_start) & (
            block["parsed_time"] <= (
                block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)]["parsed_time"].iloc[-1]
                if not block[(block["parsed_time"] > rt_start) & (block["브레이크 세기"] <= 0.1)].empty
                else block["parsed_time"].iloc[-1]
            )
        )
    ]

    if reaction_start_time == 0 and reaction_duration == 0:
        accel_mean = 0
        accel_min = 0
        accel_max = 0
    else:
        accel_mean = clipped_accel.mean()
        accel_min = accel_during_reaction.min()
        accel_max = accel_during_reaction.max()

    leader_speed_mean = block["선두 차량 속도"].mean()
    experiment_speed_mean = 100 if block["실험 차량 속도"].mean() > 99 else block["실험 차량 속도"].mean()

    bio_cols = ["GSR", "GSR_z", "SCR", "SCR_z", "PPG"]
    bio_stats = {}
    for col in bio_cols:
        avg, std, maxv, minv, rng, peaks = extract_bio_features(block[col])
        prefix = col.upper()
        bio_stats[f"{prefix}_avg"] = avg
        bio_stats[f"{prefix}_std"] = std
        bio_stats[f"{prefix}_max"] = maxv
        bio_stats[f"{prefix}_min"] = minv
        bio_stats[f"{prefix}_range"] = rng
        bio_stats[f"{prefix}_peaks"] = peaks

    return pd.Series({
        "브레이크 유형": meta["브레이크 유형"],
        "수준": meta["수준"],
        "간격": meta["간격"],
        "선두 차량 감속률": meta["감속률"],
        "실험 차량 평균 감속률": accel_mean,
        "선두 차량 평균 속도": leader_speed_mean,
        "실험 차량 평균 속도": experiment_speed_mean,
        "브레이크 평균 세기": np.nan,  # 추후 별도로 계산
        "반응 시간": reaction_start_time,
        "Reaction_Duration": reaction_duration,
        "충돌여부": collision,
        "최소 안전 거리 유지": safe_distance,
        "block_id": block_id,
        **bio_stats
    })

# --- 3. 전체 실행 코드 (6~10 포함) ---
# 경로 및 데이터 로딩
# --- 1. 데이터 불러오기 ---
fileName = "06_윤의진_merged.csv"  # 사용할 파일명 (예시)
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

# 시간 파싱 및 정렬
def parse_time(t_str):
    try:
        return datetime.strptime(t_str, "%H:%M:%S:%f")
    except:
        return None
df["parsed_time"] = df["현재 시간"].apply(parse_time)
df = df.dropna(subset=["parsed_time"]).sort_values(by="parsed_time").reset_index(drop=True)

# 블록 나누기
time_threshold = timedelta(milliseconds=1000)
block_ids = [0]
current_block = 0
for i in range(1, len(df)):
    if (df.loc[i, "parsed_time"] - df.loc[i-1, "parsed_time"]) > time_threshold:
        current_block += 1
    block_ids.append(current_block)
df["block_id"] = block_ids

# 숫자형 컬럼 처리
df["브레이크 세기"] = pd.to_numeric(df["브레이크 세기"], errors='coerce')
df["실험 차량 가속도"] = pd.to_numeric(df["실험 차량 가속도"], errors='coerce')
df["실험 차량 속도"] = pd.to_numeric(df["실험 차량 속도"], errors='coerce')
df["선두 차량 가속도"] = pd.to_numeric(df["선두 차량 가속도"], errors='coerce')
df["엑셀 세기"] = pd.to_numeric(df["엑셀 세기"], errors='coerce')

# --- 6. 통계 계산 ---
block_data = df.groupby("block_id", group_keys=False).apply(calc_block_features_with_bio).reset_index(drop=True)

# --- 7. 시도 번호 부여 ---
block_data = block_data.sort_values(by=["브레이크 유형", "수준", "간격", "선두 차량 감속률", "block_id"])
block_data["시도 번호"] = block_data.groupby(["브레이크 유형", "수준", "간격", "선두 차량 감속률"]).cumcount() + 1

# --- 8. 브레이크 평균 세기 보정 ---
def calc_brake_mean(row):
    if row["Reaction_Duration"] == 0:
        return 0
    else:
        return df[df["block_id"] == row["block_id"]]["브레이크 세기"].mean()
block_data["브레이크 평균 세기"] = block_data.apply(calc_brake_mean, axis=1)

# --- 9. 열 순서 정리 ---
bio_cols = ["GSR", "GSR_z", "SCR", "SCR_z", "PPG"]
bio_stat_cols = []
for col in bio_cols:
    prefix = col.upper()
    bio_stat_cols += [f"{prefix}_avg", f"{prefix}_std", f"{prefix}_max", f"{prefix}_min", f"{prefix}_range", f"{prefix}_peaks"]

sorted_columns = [
    "브레이크 유형", "수준", "간격", "시도 번호", "선두 차량 감속률", "실험 차량 평균 감속률",
    "선두 차량 평균 속도", "실험 차량 평균 속도", "브레이크 평균 세기",
    "반응 시간", "Reaction_Duration", "충돌여부", "최소 안전 거리 유지", "block_id"
] + bio_stat_cols

final_df = block_data[sorted_columns]

# --- 10. 정렬 및 저장 ---
brake_order = ["기본제동등A", "밝기변화제동등B", "점멸주파수변화제동등C", "면적변화제동등D"]
final_df["브레이크 유형"] = pd.Categorical(final_df["브레이크 유형"], categories=brake_order, ordered=True)
final_df = final_df.sort_values(
    by=["브레이크 유형", "수준", "간격", "선두 차량 감속률", "시도 번호"],
    ascending=[True, True, True, False, True]
).reset_index(drop=True)

output_fileName = fileName.replace("merged", "preprocessed")
output_path = os.path.join(current_folder, output_fileName)
final_df.to_csv(output_path, index=False, encoding='utf-8-sig')
