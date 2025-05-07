import random
import pandas as pd
import numpy as np

# 2차 실험 랜덤 
# 총 숫자 목록 (1~90)
numbers = list(range(1, 91))
random.shuffle(numbers)

# 3x6 배열, 총 18칸, 각 칸에 숫자 5개씩
array = []
for i in range(18):
    five_numbers = sorted(numbers[i*5:(i+1)*5])
    array.append(" ".join(map(str, five_numbers)))

# 3행 6열로 배열 재구성
array_matrix = np.array(array).reshape(3, 6)

# DataFrame으로 변환
df = pd.DataFrame(array_matrix, columns=[f"Col{i+1}" for i in range(6)], index=[f"Row{i+1}" for i in range(3)])

# 3차 실험 랜덤
# 3x3 배열, 총 9칸 × 10개 숫자 = 총 90개 숫자 사용 (1~90 전부 사용)
numbers_full = list(range(1, 91))
random.shuffle(numbers_full)

# 각 칸에 10개씩 배정
array_full_3x3 = []
for i in range(9):
    ten_numbers = sorted(numbers_full[i*10:(i+1)*10])
    array_full_3x3.append(" ".join(map(str, ten_numbers)))

# 3행 3열로 재배치
array_full_3x3_matrix = np.array(array_full_3x3).reshape(3, 3)

# DataFrame 생성
df_full_3x3 = pd.DataFrame(array_full_3x3_matrix, columns=[f"Col{i+1}" for i in range(3)], index=[f"Row{i+1}" for i in range(3)])

# 4차 실험 랜덤
# 1x4 배열, 총 4칸 × 5개 숫자 = 총 20개 숫자 사용 (1~20 전부 사용)
numbers_1x4 = list(range(1, 21))
random.shuffle(numbers_1x4)

# 각 칸에 5개씩 배정
array_1x4 = []
for i in range(4):
    five_numbers = sorted(numbers_1x4[i*5:(i+1)*5])
    array_1x4.append(" ".join(map(str, five_numbers)))

# 1행 4열 배열 생성
df_1x4 = pd.DataFrame([array_1x4], columns=[f"Col{i+1}" for i in range(4)], index=["Row1"])