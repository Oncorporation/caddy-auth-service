Dim conn, cs, shell
Set shell = CreateObject("WScript.Shell")
rem cs = "Provider=SQLOLEDB;Server=192.168.1.51,14333;Database=EncryptionDB;User Id=USER;Password=PASSWORD;TrustServerCertificate=True;"
cs = shell.ExpandEnvironmentStrings("%CADDY_AUTH_CONN%")
Set conn = CreateObject("ADODB.Connection")
conn.ConnectionString = "Provider=SQLOLEDB;" & cs
On Error Resume Next
conn.Open
If Err.Number = 0 Then
    WScript.Echo "Success"
Else
    WScript.Echo "Error: " & Err.Description
End If
conn.Close