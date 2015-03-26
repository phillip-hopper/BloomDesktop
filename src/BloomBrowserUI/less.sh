#!/usr/bin/env bash

thisDir="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

lastFile="${thisDir}/.lastLessCheck"

if ! [ -f ${lastFile} ]
then
	echo "1409590000" > "$lastFile"
fi

current=`date +%s`
last=`cat ${thisDir}/.lastLessCheck`
ext=".css"

# get all *.less files that are not in the themes directory
find ${thisDir} -type f -name *.less ! -name *themes* -print0 | while read -d $'\0' file; do

	modified=`date -r "${file}" +"%s"`

	if [ ${modified} -gt ${last} ]
	then
		destination="${file/.less/$ext}"
		#destination="${destination/less/$path}"

		# This version does some compaction also
		# ../../build/node_modules/.bin/lessc --include-path=./themes/bloom-jqueryui-theme/less --clean-css --clean-option=--s1 --clean-option=-b --no-color "$file" "$destination"

		# This version does no compaction
		../../build/node_modules/.bin/lessc --include-path=./themes/bloom-jqueryui-theme/less --no-color "$file" "$destination"

		echo "$destination"
	fi
done

echo "finished"
echo "$current" > "$lastFile"