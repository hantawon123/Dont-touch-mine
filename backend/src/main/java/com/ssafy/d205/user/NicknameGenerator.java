package com.ssafy.d205.user;

import org.springframework.stereotype.Component;

import java.util.List;
import java.util.concurrent.ThreadLocalRandom;

/**
 * 발급 시 쓸 닉네임을 만듭니다.
 *
 * <p>형용사 + 명사 + 네 자리 숫자입니다. 첫 실행에 입력 화면 없이 바로 게임에 들어갈
 * 수 있게 하려는 것이고, 마음에 들지 않으면 변경 API로 바꿉니다.
 *
 * <p>단어를 각각 4글자 이하로 골랐습니다. 최대 4 + 4 + 4 = 12글자라 닉네임 규칙의
 * 상한과 정확히 맞습니다. 단어를 추가할 때 5글자짜리를 넣으면 <b>서버가 만든 닉네임이
 * 변경 API에서 거부되는</b> 상태가 되므로 이 제약을 지켜야 합니다.
 *
 * <p>여기서 유일성을 보장하지는 않습니다. 조합이 200만 가지가 넘어 충돌이 드물기는
 * 하지만 0은 아니고, 미리 조회해 확인하는 방식은 동시 요청에 뚫립니다. 충돌은 DB의
 * uk_users_nickname이 잡고 호출부가 다른 이름으로 다시 시도합니다.
 */
@Component
public class NicknameGenerator {

    private static final List<String> ADJECTIVES = List.of(
            "용감한", "날쌘", "조용한", "씩씩한", "엉뚱한", "느긋한", "재빠른", "다정한",
            "상냥한", "부지런한", "명랑한", "신중한", "대담한", "포근한", "늠름한", "차분한"
    );

    private static final List<String> NOUNS = List.of(
            "너구리", "여우", "수달", "고양이", "판다", "두더지", "다람쥐", "올빼미",
            "고슴도치", "물개", "펭귄", "하마", "코알라", "사슴", "토끼", "늑대"
    );

    public String generate() {
        ThreadLocalRandom random = ThreadLocalRandom.current();
        String adjective = ADJECTIVES.get(random.nextInt(ADJECTIVES.size()));
        String noun = NOUNS.get(random.nextInt(NOUNS.size()));
        int number = random.nextInt(1000, 10000);
        return adjective + noun + number;
    }
}
