#!/usr/bin/env python3
"""
Measures local relief and overall contrast on game screenshots per docs/lane-1-legibility-check.md.
Local relief: mean absolute difference between adjacent horizontal and vertical pixels in central 800x450 terrain crop.
Overall contrast: standard deviation of luminance across the crop.
"""

import sys
from pathlib import Path
import numpy as np
from PIL import Image

def analyze_image(path: str):
    p = Path(path)
    if not p.is_file():
        print(f"Error: file not found: {path}", file=sys.stderr)
        sys.exit(1)
    
    img = Image.open(p).convert('L')
    w, h = img.size
    cx, cy = w // 2, h // 2
    # 800x450 central terrain crop (below UI band)
    crop = np.array(img.crop((cx - 400, cy - 225, cx + 400, cy + 225)), dtype=float)
    dx = np.abs(crop[:, 1:] - crop[:, :-1])
    dy = np.abs(crop[1:, :] - crop[:-1, :])
    local_relief = (np.mean(dx) + np.mean(dy)) / 2.0
    contrast = np.std(crop)
    return local_relief, contrast

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Usage: python measure_relief.py <image_path>")
        sys.exit(1)
    
    relief, contrast = analyze_image(sys.argv[1])
    print(f"File: {sys.argv[1]}")
    print(f"Local Relief (mean neighbor delta): {relief:.4f}")
    print(f"Overall Contrast (stdev): {contrast:.4f}")
    if relief >= 2.20:
        print("Verdict: PASS (>= 2.20 floor)")
    else:
        print("Verdict: FAIL (< 2.20 floor)")
