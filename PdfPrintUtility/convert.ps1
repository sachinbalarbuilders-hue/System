Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile('C:\Users\Admin\.gemini\antigravity-ide\brain\6ca1db9a-e7a9-46c8-bc04-66585138e451\pdf_print_logo_1780809474522.png')
$fs = [System.IO.File]::Create('c:\Users\Admin\Desktop\System\PdfPrintUtility\PdfPrintUtility\icon.ico')
$bmp = new-object System.Drawing.Bitmap($img, 256, 256)
$icon = [System.Drawing.Icon]::FromHandle($bmp.GetHicon())
$icon.Save($fs)
$fs.Close()
$img.Dispose()
$icon.Dispose()
$bmp.Dispose()
Copy-Item 'C:\Users\Admin\.gemini\antigravity-ide\brain\6ca1db9a-e7a9-46c8-bc04-66585138e451\pdf_print_logo_1780809474522.png' -Destination 'c:\Users\Admin\Desktop\System\PdfPrintUtility\PdfPrintUtility\icon.png' -Force
