import os
import sys
import pandas as pd
from datetime import datetime
import re

BASE_DIR = os.path.dirname(__file__)
DATA_DIR = os.path.join(BASE_DIR, "Unity_divided")
OUTPUT_SUFFIX = "_cleaned"
TIMESTAMP_COL = "현재 시간"
THRESHOLD_SEC = 10.0
ENCODING_CANDIDATES = ["utf-8", "utf-8-sig", "cp949"]

def parse_ts(v):
    if pd.isna(v):
        return None
    try:
        return float(v)
    except (ValueError, TypeError):
        pass
    s = str(v).strip()
    m = re.fullmatch(r'(\d{1,2}):([0-5]?\d):([0-5]?\d):(\d{1,3})', s)
    if m:
        h, mnt, sec, ms = map(int, m.groups())
        return h*3600 + mnt*60 + sec + ms/1000.0
    for fmt in ["%H:%M:%S.%f", "%H:%M:%S"]:
        try:
            dt = datetime.strptime(s, fmt)
            return dt.hour*3600 + dt.minute*60 + dt.second + dt.microsecond/1e6
        except ValueError:
            pass
    for fmt in [
        "%Y-%m-%d %H:%M:%S.%f","%Y-%m-%d %H:%M:%S",
        "%Y/%m/%d %H:%M:%S.%f","%Y/%m/%d %H:%M:%S",
        "%m/%d/%Y %H:%M:%S",
    ]:
        try:
            return datetime.strptime(s, fmt).timestamp()
        except ValueError:
            pass
    try:
        return datetime.fromisoformat(s).timestamp()
    except Exception:
        return None

def load_csv(path):
    last_err=None
    for enc in ENCODING_CANDIDATES:
        try:
            return pd.read_csv(path, encoding=enc), enc
        except Exception as e:
            last_err=e
    print(f"[ERR] 읽기 실패 {os.path.basename(path)} : {last_err}")
    return None,None

def build_segments(ts_series: pd.Series):
    gaps = ts_series.diff()
    break_points = gaps[gaps > THRESHOLD_SEC].index.tolist()
    segments=[]
    start = ts_series.index[0]
    for bp in break_points:
        segments.append((start, bp-1))
        start = bp
    segments.append((start, ts_series.index[-1]))
    # gap lengths (between segments)
    gap_lengths=[]
    for i in range(len(segments)-1):
        end_curr = segments[i][1]
        start_next = segments[i+1][0]
        gap_lengths.append(ts_series.loc[start_next]-ts_series.loc[end_curr])
    return segments, gap_lengths

def prompt_and_select(fname, segments, gap_lengths, df):
    if len(segments)==1:
        print(f"{fname} 타임스탬프 끊김 없음, 넘어감")
        return []
    gaps_txt = ", ".join(f"{g:.3f}s" for g in gap_lengths)
    print(f"{fname} 타임스탬프 끊김 있음 ({gaps_txt})")
    # 세그먼트 요약 출력
    for idx,(s,e) in enumerate(segments, start=1):
        start_ts = df.loc[s,"_ts_float"]
        end_ts = df.loc[e,"_ts_float"]
        dur = end_ts - start_ts
        print(f"  {idx}) rows={e-s+1} duration≈{dur:.3f}s")
    raw = input("어떤 데이터 선택? (1,2,... / all / Enter=skip): ").strip()
    if not raw:
        return []
    if raw.lower()=="all":
        return list(range(len(segments)))
    picks=[]
    for token in re.split(r'[\s,]+', raw):
        if token.isdigit():
            i=int(token)-1
            if 0 <= i < len(segments):
                picks.append(i)
    return sorted(set(picks))

def process_file(path):
    df, enc = load_csv(path)
    if df is None or df.empty:
        return
    if TIMESTAMP_COL not in df.columns:
        print(f"[WARN] {os.path.basename(path)} 컬럼 '{TIMESTAMP_COL}' 없음")
        return
    parsed = df[TIMESTAMP_COL].apply(parse_ts)
    if parsed.isna().all():
        print(f"[WARN] {os.path.basename(path)} 타임스탬프 파싱 실패")
        return
    df["_ts_float"]=parsed
    valid = df[~df["_ts_float"].isna()].copy()
    if valid.empty:
        print(f"[WARN] {os.path.basename(path)} 유효 타임스탬프 없음")
        return
    segments, gap_lengths = build_segments(valid["_ts_float"])
    picks = prompt_and_select(os.path.basename(path), segments, gap_lengths, valid)
    if not picks:
        return
    parts=[]
    for i in picks:
        s,e = segments[i]
        parts.append(valid.loc[s:e])
    cleaned = pd.concat(parts, axis=0).drop(columns=["_ts_float"])
    save_cleaned(path, cleaned)

def save_cleaned(orig_path, cleaned_df):
    stem, ext = os.path.splitext(os.path.basename(orig_path))
    out_name = f"{stem}{OUTPUT_SUFFIX}.csv"
    out_path = os.path.join(DATA_DIR, out_name)
    cleaned_df.to_csv(out_path, index=False, encoding="utf-8-sig")
    print(f"[SAVED] {out_name} ({len(cleaned_df)} rows)")

def main():
    if not os.path.isdir(DATA_DIR):
        print("[ERR] 폴더 없음:", DATA_DIR)
        sys.exit(1)
    files = [f for f in sorted(os.listdir(DATA_DIR))
             if f.lower().endswith(".csv") and OUTPUT_SUFFIX not in f]
    if not files:
        print("[INFO] 대상 CSV 없음")
        return
    for f in files:
        process_file(os.path.join(DATA_DIR,f))
    # process_file(os.path.join(DATA_DIR, "10_이수영_B밝기변화제동등_남자_OneToTwo_Unity.csv")) # 파일 하나 대상으로만 진행하기
    print("[DONE]")

if __name__ == "__main__":
    main()