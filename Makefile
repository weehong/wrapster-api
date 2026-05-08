.PHONY: format format-style format-all build clean restore test migrations-bundle

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

clean:
	dotnet clean

migrations-bundle:
	dotnet ef migrations bundle \
		--project src/Wrapsfer.Infrastructure \
		--startup-project src/Wrapsfer.Api \
		--output ./efbundle \
		--force
