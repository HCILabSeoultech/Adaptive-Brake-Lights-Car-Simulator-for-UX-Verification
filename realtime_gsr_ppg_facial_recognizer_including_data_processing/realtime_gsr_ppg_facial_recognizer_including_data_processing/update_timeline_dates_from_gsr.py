import re
from pathlib import Path
import pandas as pd
from collections import Counter

BASE_DIR = Path(r"c:\Users\aloho\Github\realtime_gsr_ppg_facial_recognizer_including_data_processing")
GSR_DIR = BASE_DIR / "Processed_GSR_labeled"
TIMELINE_DIR = BASE_DIR / "realtime_gsr_ppg_facial_recognizer_including_data_processing" / "timeline"
OUTPUT_DIR = TIMELINE_DIR / "date_updated"   # 새로 저장 (원본 보존)
DRY_RUN = False   # False 로 바꾸면 실제 저장
DATE_COL_CANDIDATES = ["date","Date","DATE"]

P_PREFIX_RE = re.compile(r'^(P\d+)_')

def read_gsr_date_values(p_label: str):
    """해당 P라벨 GSR 파일들의 date 컬럼 값 수집"""
    dates = []
    for f in GSR_DIR.glob(f"{p_label}_*.csv"):
        try:
            df = pd.read_csv(f, encoding="utf-8-sig")
        except UnicodeDecodeError:
            df = pd.read_csv(f, encoding="cp949")
        except Exception as e:
            print(f"[WARN] GSR 읽기 실패 {f.name}: {e}")
            continue
        date_col = next((c for c in DATE_COL_CANDIDATES if c in df.columns), None)
        if date_col is None:
            continue
        vals = df[date_col].dropna().astype(str).unique()
        dates.extend(vals)
    return dates

def pick_date(dates: list[str]) -> str | None:
    if not dates:
        return None
    # 다수결(모드) 선택, 동일 빈도 다수면 첫 등장
    cnt = Counter(dates)
    date, _ = cnt.most_common(1)[0]
    if len(cnt) > 1:
        print(f"[INFO] 여러 날짜 {dict(cnt)} -> '{date}' 선택")
    return date

def load_timeline(p_label: str):
    xlsx = TIMELINE_DIR / f"{p_label}.xlsx"
    csv  = TIMELINE_DIR / f"{p_label}.csv"
    if xlsx.exists():
        try:
            df = pd.read_excel(xlsx, header=None)
            return df, xlsx
        except Exception as e:
            print(f"[WARN] Timeline XLSX 읽기 실패 {xlsx.name}: {e}")
    if csv.exists():
        try:
            df = pd.read_csv(csv, header=None)
            return df, csv
        except Exception as e:
            print(f"[WARN] Timeline CSV 읽기 실패 {csv.name}: {e}")
    return None, None

def update_timeline_date(df: pd.DataFrame, new_date: str):
    if df.empty:
        return df
    df = df.copy()
    df.iloc[:,0] = new_date
    return df

def save_timeline(df: pd.DataFrame, src_path: Path):
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    out_path = OUTPUT_DIR / src_path.name
    if DRY_RUN:
        print(f"[DRY] {src_path.name} -> {out_path.name} 날짜 갱신 미리보기")
    else:
        if src_path.suffix.lower() == ".xlsx":
            df.to_excel(out_path, header=False, index=False)
        else:
            df.to_csv(out_path, header=False, index=False, encoding="utf-8-sig")
        print(f"[SAVE] {out_path.name}")
    return out_path

def main():
    if not GSR_DIR.is_dir():
        print("[ERR] GSR_DIR 없음")
        return
    if not TIMELINE_DIR.is_dir():
        print("[ERR] TIMELINE_DIR 없음")
        return

    timeline_files = sorted([p for p in TIMELINE_DIR.iterdir() if p.is_file() and p.stem.startswith("P")])
    if not timeline_files:
        print("[INFO] Timeline P* 파일 없음")
        return

    processed = 0
    for tfile in timeline_files:
        p_label = tfile.stem  # P1
        if not p_label[1:].isdigit():
            continue
        gsr_dates = read_gsr_date_values(p_label)
        new_date = pick_date(gsr_dates)
        if new_date is None:
            print(f"[SKIP] {p_label}: GSR 날짜 없음")
            continue
        tdf, src = load_timeline(p_label)
        if tdf is None:
            print(f"[SKIP] {p_label}: Timeline 파일 없음")
            continue
        orig_dates = tdf.iloc[:,0].dropna().astype(str).unique()
        if len(orig_dates) == 1 and orig_dates[0] == new_date:
            print(f"[OK] {p_label}: 이미 날짜 {new_date}")
            if DRY_RUN:
                continue
        updated = update_timeline_date(tdf, new_date)
        save_timeline(updated, src)
        processed += 1

    if DRY_RUN:
        print(f"\nDRY_RUN=True: {processed}개 미리보기. 실제 저장하려면 DRY_RUN=False.")
    else:
        print(f"\n완료: {processed}개 저장 (출력 폴더: {OUTPUT_DIR})")

if __name__ == "__main__":
    main()