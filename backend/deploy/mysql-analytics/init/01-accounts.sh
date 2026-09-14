#!/bin/bash
# 분석 DB 컨테이너(mysql-analytics)의 첫 기동에서 한 번 도는 초기화 (S15P21D205-980).
#
# 이 컨테이너에는 스키마가 둘입니다. MYSQL_DATABASE 로 만들어지는 d205_analytics(플레이 로그) 와
# Metabase 가 자기 설정을 저장하는 metabase. 여기서는 그 둘에 붙는 계정을 만듭니다.
#
#   d205_reader  d205_analytics 를 읽기만 하는 계정. Metabase 가 데이터를 볼 때 이 계정으로 붙습니다.
#                쓰기 권한이 없어 대시보드 SQL 을 잘못 써도 로그가 지워지지 않습니다.
#   metabase     Metabase 자신의 저장소. 자기 스키마만 갖고 플레이 로그는 못 봅니다.
#
# 앱 계정(MYSQL_USER)은 MySQL 이미지가 MYSQL_DATABASE 에 대한 전체 권한을 알아서 줍니다.
#
# 계정 서비스의 DB 에는 이 스크립트가 없습니다. 예전에는 한 컨테이너에 두 스키마가 있어 GRANT 로
# 나눴지만, 컨테이너가 갈라진 뒤로는 계정 DB 쪽에 분석 관련 계정이 있을 이유가 없습니다.
#
# 비밀번호 환경변수가 없으면 그 계정을 만들지 않고 stderr 에 적습니다. 빈 비밀번호로 만드는 것보다
# 안 만드는 편이 낫습니다. 운영 볼륨이 이미 초기화돼 있으면 이 스크립트는 돌지 않으므로, 계정을
# 뒤늦게 추가할 때는 deploy/README.md 의 수동 절차를 따릅니다.
#
# 서브셸로 감싸서 source 되든 실행되든 엔트리포인트를 죽이지 않게 합니다.
(
    set -euo pipefail

    sql() {
        mysql --protocol=socket -uroot -p"${MYSQL_ROOT_PASSWORD}"
    }

    if [ -n "${ANALYTICS_READER_PASSWORD:-}" ]; then
        sql <<EOSQL
    CREATE USER IF NOT EXISTS 'd205_reader'@'%' IDENTIFIED BY '${ANALYTICS_READER_PASSWORD}';
    ALTER USER 'd205_reader'@'%' IDENTIFIED BY '${ANALYTICS_READER_PASSWORD}';
    GRANT SELECT ON \`${MYSQL_DATABASE}\`.* TO 'd205_reader'@'%';
    FLUSH PRIVILEGES;
EOSQL
        echo "[analytics-init] d205_reader 에게 ${MYSQL_DATABASE} 의 SELECT 권한을 주었습니다."
    else
        echo "[analytics-init] ANALYTICS_READER_PASSWORD 가 없어 d205_reader 를 만들지 않습니다." >&2
    fi

    if [ -n "${METABASE_DB_PASSWORD:-}" ]; then
        sql <<EOSQL
    CREATE DATABASE IF NOT EXISTS \`metabase\` CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
    CREATE USER IF NOT EXISTS 'metabase'@'%' IDENTIFIED BY '${METABASE_DB_PASSWORD}';
    ALTER USER 'metabase'@'%' IDENTIFIED BY '${METABASE_DB_PASSWORD}';
    GRANT ALL PRIVILEGES ON \`metabase\`.* TO 'metabase'@'%';
    FLUSH PRIVILEGES;
EOSQL
        echo "[analytics-init] metabase 스키마와 계정을 준비했습니다."
    else
        echo "[analytics-init] METABASE_DB_PASSWORD 가 없어 metabase 계정을 만들지 않습니다." >&2
    fi
)
