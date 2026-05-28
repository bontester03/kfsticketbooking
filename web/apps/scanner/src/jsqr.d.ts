// jsQR is loaded from a CDN <script> in index.html (the proxy blocks bundling it), so it's a
// global. Minimal ambient typing for what we use.
interface JsQrResult {
  data: string;
}
declare function jsQR(
  data: Uint8ClampedArray,
  width: number,
  height: number,
  options?: { inversionAttempts?: 'dontInvert' | 'onlyInvert' | 'attemptBoth' | 'invertFirst' }
): JsQrResult | null;
