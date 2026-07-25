# SecureShop GitHub Actions

## CI

`.github/workflows/ci.yml`:

- Wird bei Pull Requests gegen `main` ausgeführt.
- Wird bei Pushes auf Branches außerhalb von `main` ausgeführt.
- Stellt Abhängigkeiten wieder her, baut im Release-Modus und führt alle Tests aus.
- Speichert Testergebnisse und Coverage-Dateien 14 Tage als Artifact.

## CD

`.github/workflows/cd.yml` startet bei einem Push auf `main` oder manuell und
führt zuerst die CI-Prüfung aus. Anschließend werden veröffentlichungsfertige
Pakete für API und MVC erzeugt und sieben Tage als GitHub Actions Artifacts
gespeichert.

Beim Push eines Git-Tags mit dem Präfix `v`, zum Beispiel `v1.0.1`, werden die
Pakete automatisch als `.tar.gz` archiviert und einem GitHub Release hinzugefügt.

Für diesen Ablauf werden weder Repository Secrets noch Variables oder ein
kostenpflichtiger Cloud-Dienst benötigt. Der Release-Vorgang verwendet das von
GitHub automatisch bereitgestellte `GITHUB_TOKEN`.

Beispiel für eine Veröffentlichung:

```bash
git tag -a v1.0.1 -m "SecureShop v1.0.1"
git push origin v1.0.1
```
