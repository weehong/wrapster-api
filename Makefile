.PHONY: format format-style format-all build clean restore test migrate run migrations-bundle

WRAPSFER_DESIGN_CONNECTION ?= Host=localhost;Port=5432;Database=wrapsfer;Username=vernon;Password=password

# Run whitespace/formatting fixes
format:
	dotnet format --no-restore

# Run code-style fixes (var -> explicit types, etc.)
format-style:
	dotnet format style --no-restore

# Run both format and style in sequence
format-all: format format-style

# Restore, build, test
restore:
	dotnet restore

build:
	dotnet build --no-restore

test:
	dotnet test --no-restore --no-build

migrate:
	WRAPSFER_DESIGN_CONNECTION="$(WRAPSFER_DESIGN_CONNECTION)" dotnet ef database update \
		--project src/Wrapsfer.Infrastructure/Wrapsfer.Infrastructure.csproj \
		--startup-project src/Wrapsfer.Api/Wrapsfer.Api.csproj

run:
	dotnet run --project src/Wrapsfer.Api/Wrapsfer.Api.csproj

clean:
	dotnet clean

migrations-bundle:
	dotnet ef migrations bundle \
		--project src/Wrapsfer.Infrastructure \
		--startup-project src/Wrapsfer.Api \
		--output ./efbundle \
		--force
