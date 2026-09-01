# 빌드를 이미지 안에서 합니다. 개발자 PC에 JDK 21이 없어도 되고,
# 누가 어디서 빌드하든 같은 jar이 나옵니다.
FROM eclipse-temurin:21-jdk AS build
WORKDIR /workspace

# 래퍼와 의존성 선언을 먼저 복사해서 한 레이어로 굳힙니다. 소스만 고친
# 재배포에서 의존성 다운로드를 통째로 캐시에서 재사용하기 위해서입니다.
# 이걸 안 하면 한 줄만 고쳐도 매번 수백 MB를 다시 받습니다.
COPY gradlew ./
COPY gradle gradle
COPY build.gradle settings.gradle ./
RUN chmod +x gradlew && ./gradlew --no-daemon --quiet dependencies

COPY src src
# 테스트는 CI에서 돌립니다. 배포 이미지 빌드는 산출물을 만드는 단계입니다.
RUN ./gradlew --no-daemon bootJar -x test

FROM eclipse-temurin:21-jre
WORKDIR /app

# 애플리케이션을 root로 돌릴 이유가 없습니다.
RUN useradd --system --create-home --shell /usr/sbin/nologin app
COPY --from=build /workspace/build/libs/*.jar app.jar
RUN chown app:app app.jar
USER app

EXPOSE 8080

# 컨테이너 메모리를 기준으로 힙을 잡게 합니다. 고정값을 박으면
# 인스턴스를 바꿀 때마다 따라다니며 고쳐야 합니다.
ENV JAVA_OPTS="-XX:MaxRAMPercentage=75"
ENTRYPOINT ["sh", "-c", "exec java $JAVA_OPTS -jar /app/app.jar"]
