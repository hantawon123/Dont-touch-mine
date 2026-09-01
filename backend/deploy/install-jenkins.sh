#!/usr/bin/env bash
# Jenkins 설치. EC2에서 실행합니다. 여러 번 실행해도 안전합니다.
#
# 컨테이너가 아니라 호스트에 apt로 설치합니다. docker와 compose가 이미 호스트에
# 있으므로, Jenkins 컨테이너에 docker CLI를 넣고 /var/run/docker.sock의 그룹
# 권한을 맞추는 것보다 실패 지점이 적습니다.
#
# 주의: 이 스크립트가 끝난 직후 Jenkins는 기동에 실패한 상태입니다.
# 기본 포트 8080을 앱 컨테이너가 이미 쓰고 있기 때문입니다. 포트와 프리픽스를
# 지정하는 systemd 드롭인을 넣은 뒤에 정상 기동합니다. 의도된 순서입니다.
set -euo pipefail

# 2026년 기준 debian-stable 저장소의 서명 키입니다.
# jenkins.io-2023.key는 키 ID가 5BA31D57EF5975CA로 더 이상 맞지 않습니다.
JENKINS_KEY_URL='https://pkg.jenkins.io/debian-stable/jenkins.io-2026.key'
JENKINS_KEY_FPR='5E386EADB55F01504CAE8BCF7198F4B714ABFC68'

echo "== 1/6 Java 21 =="
sudo apt-get update -qq || true
sudo apt-get install -y -qq fontconfig openjdk-21-jre gnupg curl

echo "== 2/6 서명 키 =="
sudo install -m 0755 -d /usr/share/keyrings
sudo curl -fsSL "$JENKINS_KEY_URL" -o /usr/share/keyrings/jenkins-keyring.asc
sudo chmod a+r /usr/share/keyrings/jenkins-keyring.asc

echo "== 3/6 키 지문 검증 =="
# 이 검증이 없으면 키가 교체됐을 때 apt-get update의 NO_PUBKEY 오류로만
# 드러납니다. 원인이 키인지 저장소인지 구분이 안 됩니다.
if gpg --show-keys --with-colons /usr/share/keyrings/jenkins-keyring.asc | grep -q "$JENKINS_KEY_FPR"; then
    echo "지문 일치: $JENKINS_KEY_FPR"
else
    echo "지문이 예상과 다릅니다. Jenkins가 키를 또 교체했을 수 있습니다." >&2
    gpg --show-keys /usr/share/keyrings/jenkins-keyring.asc >&2
    exit 1
fi

echo "== 4/6 저장소 등록 및 Jenkins 설치 =="
printf 'deb [signed-by=/usr/share/keyrings/jenkins-keyring.asc] https://pkg.jenkins.io/debian-stable binary/\n' \
  | sudo tee /etc/apt/sources.list.d/jenkins.list > /dev/null
sudo apt-get update -qq
sudo apt-get install -y -qq jenkins \
  || echo "(설치 후 기동 실패는 8080 충돌 때문입니다. 다음 단계에서 해결합니다.)"

echo "== 5/6 docker 그룹 =="
# Jenkins가 docker compose를 실행해야 합니다. 이 권한은 사실상 root와 동등하므로
# Jenkins UI 보안(관리자 비밀번호, 익명 접근 차단)이 반드시 뒤따라야 합니다.
sudo usermod -aG docker jenkins

echo "== 6/6 상태 및 유닛 정보 =="
echo "-- 버전 --"
dpkg-query -W -f='jenkins ${Version}\n' jenkins || true
echo "-- 서비스 --"
systemctl is-enabled jenkins || true
systemctl is-active jenkins || true
echo
echo "-- systemd 유닛의 ExecStart / Environment (드롭인 작성에 필요) --"
systemctl cat jenkins | grep -nE '^\[|ExecStart|Environment' || true
echo
echo "-- 리스닝 포트 --"
ss -tln | grep -E ':(8080|9090) ' || echo "(8080/9090 리스닝 없음)"
