#!/bin/sh
set -eu

config_file="${CHAPTARR_CONFIG_FILE:-/config/config.xml}"
port="${Chaptarr__Server__Port:-}"
url_base="${Chaptarr__Server__UrlBase:-}"

if [ -r "$config_file" ]; then
    if [ -z "$port" ]; then
        port="$(sed -n 's/.*<Port>\([0-9][0-9]*\)<\/Port>.*/\1/p' "$config_file" | head -n 1)"
    fi
    if [ -z "$url_base" ]; then
        url_base="$(sed -n 's/.*<UrlBase>\([^<]*\)<\/UrlBase>.*/\1/p' "$config_file" | head -n 1)"
    fi
fi

port="${port:-${CHAPTARR_DEFAULT_PORT:-8789}}"
case "$port" in
    ''|*[!0-9]*)
        echo "Invalid Chaptarr HTTP port for Docker health check." >&2
        exit 1
        ;;
esac

url_base="${url_base#/}"
url_base="${url_base%/}"
if [ -n "$url_base" ]; then
    url_base="/$url_base"
fi

exec wget --quiet --output-document=/dev/null "http://127.0.0.1:${port}${url_base}/ping"
