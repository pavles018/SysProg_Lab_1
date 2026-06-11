$url = "http://localhost:8080/api.php?amount=10&category=25&difficulty=medium"

$jobs = @()

for ($i = 1; $i -le 20; $i++) {
    $jobs += Start-Job -ScriptBlock {
        param($u)

        try {
            Invoke-WebRequest -Uri $u -UseBasicParsing | Out-Null
            "OK"
        }
        catch {
            "GRESKA: $($_.Exception.Message)"
        }
    } -ArgumentList $url
}

Wait-Job -Job $jobs | Out-Null

Receive-Job -Job $jobs

Remove-Job -Job $jobs