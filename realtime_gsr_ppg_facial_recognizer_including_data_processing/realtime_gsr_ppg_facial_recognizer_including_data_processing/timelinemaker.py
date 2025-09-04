import argparse
import re
from datetime import date
from pathlib import Path
import pandas as pd

# 설정
BASE_DIR = Path(__file__).parent
SOURCE_DIR = BASE_DIR / "Unity_divided_cleaned_labeled"
OUT_DIR = BASE_DIR / "timeline"
TIMESTAMP_COL = "현재 시간"
ENCODINGS = ["utf-8-sig", "utf-8", "cp949"]

P_PREFIX_RE = re.compile(r'^(P\d+)_')   # P번호 추출 (ABCD 조건 무시)

def read_csv_any(path: Path) -> pd.DataFrame | None:
    for enc in ENCODINGS:
        try:
            return pd.read_csv(path, encoding=enc)
        except Exception:
            continue
    print(f"[WARN] 읽기 실패: {path.name}")
    return None

def normalize_time(s: str) -> str:
    # HH:MM:SS:ms -> HH:MM:SS
    if not isinstance(s, str):
        s = str(s)
    s = s.strip()
    m = re.fullmatch(r'(\d{1,2}:[0-5]?\d:[0-5]?\d):\d{1,3}', s)
    if m:
        return m.group(1)
    m2 = re.fullmatch(r'(\d{1,2}:[0-5]?\d:[0-5]?\d)(\.\d+)?', s)
    if m2:
        return m2.group(1)
    return s

def collect_groups() -> dict[str, list[Path]]:
    groups = {}
    for f in SOURCE_DIR.glob("P*.csv"):
        if not f.is_file():
            continue
        m = P_PREFIX_RE.match(f.name)
        if not m:
            continue
        p_label = m.group(1)  # P1, P2 ...
        groups.setdefault(p_label, []).append(f)
    return groups

def get_group_time_range(p_label: str, files: list[Path]):
    """
    그룹 내 여러 CSV의 시작/끝 시간 중
    가장 이른 시작, 가장 늦은 끝 (ABCD 구분 안 함).
    cleaned 우선이 아닌 '전체 범위' 방식.
    """
    first_times = []
    last_times = []
    for f in sorted(files):
        df = read_csv_any(f)
        if df is None or df.empty or TIMESTAMP_COL not in df.columns:
            continue
        col = df[TIMESTAMP_COL].dropna()
        if col.empty:
            continue
        first_times.append(str(col.iloc[0]))
        last_times.append(str(col.iloc[-1]))
    if not first_times or not last_times:
        return None, None
    # 문자열 비교로도 가능하지만 안정 위해 HH:MM:SS:ms -> 초 변환
    def to_seconds(raw: str):
        raw = raw.strip()
        m = re.fullmatch(r'(\d{1,2}):([0-5]?\d):([0-5]?\d):(\d{1,3})', raw)
        if m:
            h, mi, se, ms = map(int, m.groups())
            return h*3600+mi*60+se+ms/1000
        m2 = re.fullmatch(r'(\d{1,2}):([0-5]?\d):([0-5]?\d)', raw)
        if m2:
            h, mi, se = map(int, m2.groups())
            return h*3600+mi*60+se
        return None
    # 매핑 (원본, 초)
    first_pairs = [(t, to_seconds(t)) for t in first_times]
    last_pairs = [(t, to_seconds(t)) for t in last_times]
    # 초값 없는 것은 문자열 사전순 fallback
    def pick_earliest(pairs):
        valid = [p for p in pairs if p[1] is not None]
        if valid:
            return min(valid, key=lambda x: x[1])[0]
        return min(pairs, key=lambda x: x[0])[0]
    def pick_latest(pairs):
        valid = [p for p in pairs if p[1] is not None]
        if valid:
            return max(valid, key=lambda x: x[1])[0]
        return max(pairs, key=lambda x: x[0])[0]
    earliest = normalize_time(pick_earliest(first_pairs))
    latest = normalize_time(pick_latest(last_pairs))
    return earliest, latest

def write_timeline(p_label: str, out_date: str, start_time: str, end_time: str):
    OUT_DIR.mkdir(parents=True, exist_ok=True)
    out_df = pd.DataFrame([
        [out_date, start_time, "event1 start"],
        [out_date, end_time, "event1 end"]
    ])
    out_file = OUT_DIR / f"{p_label}.xlsx"
    out_df.to_excel(out_file, header=False, index=False)
    print(f"[OK] {p_label}.xlsx  {start_time} ~ {end_time}")

def main():
    parser = argparse.ArgumentParser(description="Timeline maker (ABCD 무시)")
    parser.add_argument("--date", dest="date_str", default=str(date.today()),
                        help="타임라인 날짜 (YYYY-MM-DD)")
    parser.add_argument("--only", nargs="*", help="특정 P라벨만 (예: P1 P3)")
    args = parser.parse_args()

    groups = collect_groups()
    if not groups:
        print("대상 파일 없음")
        return

    targets = groups.keys()
    if args.only:
        only_set = set(args.only)
        targets = [p for p in targets if p in only_set]

    for p_label in sorted(targets, key=lambda x: int(x[1:])):
        start_time, end_time = get_group_time_range(p_label, groups[p_label])
        if not start_time or not end_time:
            print(f"[SKIP] 시간 추출 실패: {p_label}")
            continue
        write_timeline(p_label, args.date_str, start_time, end_time)

    print("[DONE]")

if __name__ == "__main__":
    main()