.PHONY: package

package:
	rm -rf pkg/*
	dotnet pack
	mkdir -p pkg
	find ./ ! -path pkg -name "*.nupkg" -exec mv {} ${PWD}/pkg \;

publish: package check-env
	sh ./.scripts/publish.sh
	
check-env:
ifndef NUGET_API_KEY
	$(error NUGET_API_KEY is undefined)
endif