<!-- SPDX-License-Identifier: Apache-2.0 -->
<!-- Copyright (c) 2026 Amir Farhadi -->

[ 🏠 ADCE Home ](../../README.md) › [ 📚 Documentation Hub ](../CONTEXT.md) › [ 🔬 External Research ](README.md) › **TIRG-DLL & Text Geometry Audit**

---

# External Research: `TIRG-DLL`, Text Geometry & Spatial Voice Navigation

> **Document Status:** Historical Research Archive / Computer Vision Audit
> **Epistemic Authority:** Tier 6 (External Research & Upstream Lineage — Non-Normative Background Context)
> **Normative Baseline:** For active architectural contracts, consult [docs/CONTEXT.md](../CONTEXT.md) and [docs/architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md](../architecture/UI_AUTOMATION_STRUCTURES_REFERENCE.md).
> **Scope:** Technical audit of [LexiconCode/tirg-dll](https://github.com/LexiconCode/tirg-dll/tree/CMake) (Text Information Retrieval and Geometry DLL) and the core TRG computer-vision algorithm.
> **Key Premise:** Analyzing why bounding boxes with text coordinates are fundamental accessibility primitives, and how fast spatial segmentation bridges gaps where standard UI Automation trees are missing or non-functional.

---

## 1. Executive Summary & The Visual Grounding Problem

In desktop automation and accessibility, extracting **what** text is on screen is only half the battle—knowing **where** that text is physically located (its bounding box `Rect { x1, y1, x2, y2 }`) is essential for:
1. **Voice-to-Click Navigation:** Directing the mouse cursor to interactive labels, buttons, or custom hyperlinks by voice (e.g. Caster's mouse grid, click-by-name, or eye-tracking + voice confirmation).
2. **Visual Overlay Alignment:** Anchoring diagnostic HUD overlays, bounding borders, and tooltips directly over targeted UI elements without displacing surrounding UI.
3. **Accessibility Fallback for Custom Canvases:** Bridging applications built with non-standard immediate-mode renderers (Flutter, WebGL, raw HTML5 `<canvas>`, Blender, Figma, legacy games, remote desktop streams) where Windows UI Automation reports `DesktopSemanticZone.Unknown` or returns an empty accessibility subtree.

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                        TEXT BOUNDING BOX EXTRACTION SPECTRUM                           │
├────────────────────────────────┬───────────────────────────────────────────────────────┤
│ Approach                       │ Characteristics & Latency Profile                     │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ 1. UIA `BoundingRectangle`     │ • Microsecond COM read when supported                 │
│    (Native Windows Controls)   │ • ❌ Fails on custom canvases, WebGL, games, Flutter   │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ 2. Neural OCR (Tesseract/Easy) │ • Full character text + bounding boxes                │
│    (Deep Learning Pipeline)    │ • ❌ Heavy latency: 200 ms – 1,000 ms per frame        │
│                                │ • ❌ High CPU/GPU load (unsuitable for 60 FPS loop)   │
├────────────────────────────────┼───────────────────────────────────────────────────────┤
│ 3. TIRG Algorithm (TRG C++ DLL)│ • Fast image-processing bounding box finder           │
│    (Lightweight Pixel Matrix)  │ • ✅ Sub-10ms execution on raw RGB pixel buffers       │
│                                │ • ✅ Zero neural network overhead, pure math/geometry  │
│                                │ • ✅ Returns exact `(x1, y1, x2, y2)` text cluster rects│
└────────────────────────────────┴───────────────────────────────────────────────────────┘
```

---

## 2. Reverse Engineering the TRG Algorithm (`trg.hpp`)

`LexiconCode/tirg-dll` provides a high-performance C++ implementation exporting two minimal C-linkage entrypoints:
```c
char* getTextBBoxesFromBytes(char* b, int w, int h);
char* getTextBBoxesFromFile(char* path, int w, int h);
```

Both functions process raw 24-bit RGB pixel buffers and return a comma-delimited string of integer bounding rectangles:
$$R = \{(x_{1}, y_{1}, x_{2}, y_{2})_{1}, (x_{1}, y_{1}, x_{2}, y_{2})_{2}, \dots\}$$

### 2.1 The 5-Stage Image Processing Pipeline

```
Raw RGB Framebuffer ──► [1. ITU-R Luma Conversion] ──► [2. Adaptive Contrast Matrix]
                                                              │
                                                              ▼
[5. Grouping & BBoxes] ◄── [4. 8-Neighbor Connectivity] ◄── [3. Regional Thresholding]
```

1. **ITU-R 601-2 Luma Transformation:**
   Converts 24-bit RGB color pixels to perceptual 8-bit luminance ($Y$) using standard photometric weighting:
   $$Y = 0.299 \cdot R + 0.587 \cdot G + 0.114 \cdot B$$

2. **Adaptive Contrast & Regional Variance Mapping (`calc_lums`):**
   The image is partitioned into spatial sub-zones ($zw = w / 3$, $zh = h / 1$). For each sub-zone, it computes the regional mean luminance $S_r$ and mean deviation $C_c$, deriving an adaptive local contrast matrix $cyx[y][x] = \max(35.0, C_c)$.

3. **Regional Edge Density & Thresholding:**
   Detects steep gradient changes characteristic of typographic stroke edges. It filters out solid background fills and gradient washes by comparing local pixel variances against the dynamic threshold.

4. **8-Directional Neighborhood Connectivity Analysis (`d8`):**
   Scans connected pixel clusters across 8 discrete directional offsets:
   $$\{ (1, 1), (1, 0), (1, -1), (0, -1), (-1, -1), (-1, 0), (-1, 1), (0, 1) \}$$
   Connected text strokes within character height bounds ($h_{\min} = 6\text{ px}, h_{\max} = 66\text{ px}$) are grouped into glyph clusters.

5. **Bounding Box Consolidation & Merging:**
   Horizontal and vertical bounding rectangles within typographic proximity ($dw = 24\text{ px}, dh = 24\text{ px}$) are merged into cohesive word and line bounding boxes (`Rect { x1, y1, x2, y2 }`).

---

## 3. Why Bounding Boxes with Text Matter for Voice & Accessibility

As noted by `lexiconcode`, having the **bounding box with text** is a cornerstone of robust accessibility architectures:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        SPATIAL RECTANGLE CONTEXT                       │
├────────────────────────────────────────────────────────────────────────┤
│ Visual Screen Coordinates:                                             │
│                                                                        │
│  (100, 200) ┌────────────────────────┐ (320, 200)                      │
│             │  [ Submit Pull Request ]│                                │
│  (100, 240) └────────────────────────┘ (320, 240)                      │
│                                                                        │
│ Spatial Metadata Envelope:                                             │
│ • Bounding Box: { Left: 100, Top: 200, Right: 320, Bottom: 240 }       │
│ • Center Click Target: (210, 220)                                      │
│ • Aspect Ratio: 5.5:1 (Button / Text Line)                             │
│ • Semantic Zone: FormSubmissionBar                                     │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Key Advantages Over Pure Textual Scraping:
1. **Direct Clickability (Target Acquisition):** If an agent or voice user knows the phrase `"Submit Pull Request"` is on screen, they cannot interact with it without the $(X, Y)$ coordinate. A text bounding box gives the exact click centroid $(x_1 + x_2)/2, (y_1 + y_2)/2$.
2. **Spatial Disambiguation:** If the word `"Delete"` appears 5 times on screen (e.g. next to 5 table rows), bounding box coordinates allow disambiguating commands like `"click delete top"`, `"click delete line 3"`, or clicking the item closest to the current mouse cursor.
3. **Zonal Clustered Partitioning:** Bounding box clusters naturally segment the UI into functional zones (header bar, sidebar navigation, main content table, footer action row) purely through geometric spatial clustering, even when the underlying DOM structure is completely obscured.

---

## 4. Integration Opportunities with ADCE & Caster

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                         HYBRID UIA + GEOMETRY INTEGRATION                              │
├────────────────────────────────────────────────────────────────────────────────────────┤
│                     [Active Foreground Window (HWND)]                                  │
│                                     │                                                  │
│                     ┌───────────────┴───────────────┐                                  │
│                     ▼                               ▼                                  │
│          [Primary: FlaUI.UIA3]             [Secondary / Fallback]                      │
│        • Reads structured UIA tree         • Captures HWND Client Bitmap               │
│        • Fast < 15ms batch cache           • Runs TIRG DLL C++ BBox finder             │
│        • High semantic fidelity            • Extracts visual text rects in < 8ms       │
│                     │                               │                                  │
│                     └───────────────┬───────────────┘                                  │
│                                     ▼                                                  │
│                    [Unified Spatial Context Envelope]                                  │
│                    • Semantic Zone: Editor / Canvas / Terminal                         │
│                    • Elements: [ { Text, BoundingBox, ClickTarget } ]                  │
│                                     │                                                  │
│                                     ▼                                                  │
│                    [MCP Server & Caster Voice Bridge]                                  │
│                    • Voice Command: "click <target>"                                   │
│                    • Caster HUD: Anchors highlights at exact bounding rects            │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

### 4.1 Concrete Integration Patterns:
1. **Fallback Visual Sensor in `ADCE.Extraction`:**
   When `FlaUI.UIA3` encounters an inaccessible custom window (e.g. `DesktopSemanticZone.Unknown` or Chromium with disabled accessibility trees), ADCE can capture a fast BitBlt framebuffer of the window and invoke `tirg-64.dll` to return all clickable text zones.
2. **Caster HUD Diagnostic Visualizer:**
   The Caster Heads-Up Display (PyQt6 / QSS overlay) can project bounding boxes over recognized text zones, providing visual feedback to the user regarding what elements the voice engine currently sees.

---

## 5. Architectural Verdict & Synthesis

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│                              TIRG-DLL EVALUATION MATRIX                                │
├──────────────────────────────┬──────────────────────────────┬──────────────────────────┤
│ Dimension                    │ TIRG-DLL (TRG C++)           │ Standard OCR (Tesseract) │
├──────────────────────────────┼──────────────────────────────┼──────────────────────────┤
│ **Latency**                  │ **3 ms – 10 ms** (Real-time) │ 250 ms – 1,200 ms (Lag)  │
│ **Dependencies**             │ `KERNEL32.dll` (Zero deps)   │ Leptonica, Tesseract libs│
│ **Binary Footprint**         │ **~290 KB** (`tirg-64.dll`)  │ 30 MB – 100 MB+ models   │
│ **Output**                   │ Bounding Rectangles `(x,y)`  │ Text strings + BBoxes    │
│ **CPU Consumption**          │ Negligible single-thread scan│ High multi-core CPU/GPU  │
└──────────────────────────────┴──────────────────────────────┴──────────────────────────┘
```

* **Verdict:** 🟢 **High Value as Spatial Primitive & UIA Fallback.** `tirg-dll` provides a zero-overhead, highly optimized visual bounding box extractor that complements ADCE's primary UIA caching pipeline.
