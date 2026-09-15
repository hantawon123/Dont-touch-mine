# 두 서비스가 같은 Dockerfile 을 씁니다. 빌드 인자 MODULE 이 app 인지 analytics 인지 정합니다.
#
#   docker build --build-arg MODULE=app -t d205-app .
#   docker build --build-arg MODULE=analytics -t d205-analytics .
#
# compose.prod.yml 이 서비스마다 그 인자를 넘깁니다. 파일을 둘로 나누지 않은 이유는 두 이미지의
# 만드는 법이 모듈 이름 말고는 같기 때문입니다. 나누면 한쪽만 고치는 날이 옵니다.
#
# 빌드를 이미지 안에서 합니다. 개발자 PC에 JDK 21이 없어도 되고, 누가 어디서 빌드하든 같은 jar이 나옵니다.
FROM eclipse-temurin:21-jdk AS build
ARG MODULE
WORKDIR /workspace

# 래퍼와 의존성 선언을 먼저 복사해서 한 레이어로 굳힙니다. 소스만 고친 재배포에서 의존성 다운로드를
# 통째로 캐시에서 재사용하기 위해서입니다. 멀티모듈이라 각 모듈의 build.gradle 도 여기서 함께
# 복사해야 의존성 해석이 됩니다.
COPY gradlew ./
COPY gradle gradle
COPY build.gradle settings.gradle ./
COPY common/build.gradle common/
COPY app/build.gradle app/
COPY analytics/build.gradle analytics/
# stdout만 버립니다. dependencies 태스크는 의존성 트리를 결과물로 출력하는데, 목적은 다운로드로
# 레이어를 굳히는 것이라 트리는 필요 없습니다. stderr는 남겨서 실패 원인이 보이게 합니다.
RUN chmod +x gradlew && ./gradlew --no-daemon :${MODULE}:dependencies > /dev/null

# common 은 두 모듈이 다 쓰므로 늘 복사합니다. 상대 모듈은 복사하지 않아 그쪽 소스가 바뀌어도
# 이 이미지의 캐시가 깨지지 않습니다.
COPY common common
# 분석 서비스는 docs/analytics-dashboards.md 를 빌드 때 리소스로 싣습니다(S15P21D205-976). 빠지면
# 이미지는 만들어지고 컨테이너가 기동에서 죽습니다. analytics/build.gradle 이 파일이 없으면 빌드를 멈추게
# 검사하지만, 그 검사가 도는 것도 이 COPY 가 있어야 합니다.
COPY docs docs
COPY ${MODULE} ${MODULE}
# 테스트는 CI에서 돌립니다. 배포 이미지 빌드는 산출물을 만드는 단계입니다.
RUN ./gradlew --no-daemon :${MODULE}:bootJar -x test

FROM eclipse-temurin:21-jre
# ARG 는 스테이지마다 다시 선언해야 보입니다.
ARG MODULE
WORKDIR /app

# 애플리케이션을 root로 돌릴 이유가 없습니다.
RUN useradd --system --create-home --shell /usr/sbin/nologin app
COPY --from=build /workspace/${MODULE}/build/libs/*.jar app.jar
RUN chown app:app app.jar
USER app

EXPOSE 8080

# 컨테이너 메모리를 기준으로 힙을 잡게 합니다. compose 의 mem_limit 이 곧 이 값의 기준이라,
# 상한을 바꾸면 힙도 따라갑니다. 고정값을 박으면 상한을 바꿀 때마다 따라다니며 고쳐야 합니다.
ENV JAVA_OPTS="-XX:MaxRAMPercentage=75"
ENTRYPOINT ["sh", "-c", "exec java $JAVA_OPTS -jar /app/app.jar"]
