$ErrorActionPreference = 'Stop'
$bundle = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot 'bin\Debug\net10.0\browser-wasm\AppBundle'))
if (-not (Test-Path (Join-Path $bundle 'index.html'))) { throw "Build ArcadeDodger.Browser first; bundle missing at $bundle" }
$root = $bundle.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
$listener = [Net.HttpListener]::new()
$listener.Prefixes.Add('http://localhost:54955/')
$listener.Start()
Write-Host 'Arcade Dodger browser build: http://localhost:54955/ (Ctrl+C to stop)'
$types = @{ '.html'='text/html; charset=utf-8'; '.js'='text/javascript; charset=utf-8'; '.json'='application/json'; '.wasm'='application/wasm'; '.dll'='application/octet-stream'; '.dat'='application/octet-stream'; '.data'='application/octet-stream'; '.css'='text/css; charset=utf-8' }
try {
  while ($listener.IsListening) {
    $context = $listener.GetContext()
    try {
      $relative = [Uri]::UnescapeDataString($context.Request.Url.AbsolutePath.TrimStart('/'))
      if (-not $relative) { $relative = 'index.html' }
      $path = [IO.Path]::GetFullPath((Join-Path $bundle $relative))
      if (-not $path.StartsWith($root, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path $path -PathType Leaf)) {
        $context.Response.StatusCode = 404
      } else {
        $ext = [IO.Path]::GetExtension($path).ToLowerInvariant()
        if ($types.ContainsKey($ext)) { $context.Response.ContentType = $types[$ext] }
        $bytes = [IO.File]::ReadAllBytes($path)
        $context.Response.ContentLength64 = $bytes.Length
        $context.Response.OutputStream.Write($bytes, 0, $bytes.Length)
      }
    } finally { $context.Response.Close() }
  }
} finally { $listener.Stop(); $listener.Close() }
