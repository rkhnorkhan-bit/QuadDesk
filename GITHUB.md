# Публикация QuadDesk в GitHub

## Первый push

```powershell
git init
git add .
git commit -m "QuadDesk v0.1.5: installer, icon and release pipeline"
git branch -M main
git remote add origin https://github.com/OWNER/QuadDesk.git
git push -u origin main
```

## Релиз

Проверьте, что `Directory.Build.props` содержит нужную версию, затем:

```powershell
git tag v0.1.5
git push origin v0.1.5
```

GitHub Actions соберёт Windows x64 installer, portable ZIP и SHA-256 и прикрепит их к GitHub Release.

## Следующие изменения

```powershell
git pull --ff-only
# внести изменения
.\publish.ps1
git add .
git commit -m "описание изменения"
git push
```

Не коммитьте `artifacts/`, `bin/`, `obj/`, `config.json`, `logs/` и `workspaces/`.
