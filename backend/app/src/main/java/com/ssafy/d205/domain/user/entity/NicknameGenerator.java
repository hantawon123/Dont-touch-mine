package com.ssafy.d205.domain.user.entity;

import org.springframework.stereotype.Component;

import java.util.List;
import java.util.concurrent.ThreadLocalRandom;

/**
 * 발급 시 쓸 닉네임을 만듭니다.
 *
 * <p>형용사 + 동물 + 네 자리 숫자입니다({@code BoldFox4821}). 첫 실행에 입력 화면 없이 바로 게임에
 * 들어갈 수 있게 하려는 것이고, 마음에 들지 않으면 변경 API로 바꿉니다.
 *
 * <p>2026-09-16 에 한글에서 영문으로 바꿨습니다(S15P21D205-1017). 영어 사용자가 첫 화면에서 읽을 수
 * 없는 이름을 받는 것을 피하려는 것이고, 한글 사용자에게는 임시 이름이라는 것이 더 분명해집니다.
 *
 * <p>단어를 각각 4글자 이하로 골랐습니다. 최대 4 + 4 + 4 = 12글자라 닉네임 규칙의 상한과 정확히
 * 맞습니다. 단어를 추가할 때 5글자짜리를 넣으면 <b>서버가 만든 닉네임이 변경 API에서 거부되는</b>
 * 상태가 되므로 이 제약을 지켜야 합니다. 첫 글자만 대문자라 단어 경계가 보입니다.
 *
 * <p><b>동물 목록은 클라이언트의 스트리머 모드 가명({@code Assets/_Game/Core/Settings/Pseudonym.cs})과
 * 같은 단어입니다.</b> 가명이 기본 닉네임과 구분되지 않게 하려는 것이 그쪽의 의도라, 한쪽 목록을
 * 고치면 다른 쪽도 같이 고칩니다. 두 코드베이스라 파일을 공유할 방법은 없습니다.
 *
 * <p>여기서 유일성을 보장하지는 않습니다. 조합이 230만 가지라 충돌이 드물기는 하지만 0은 아니고,
 * 미리 조회해 확인하는 방식은 동시 요청에 뚫립니다. 충돌은 DB의 uk_users_nickname이 잡고 호출부가
 * 다른 이름으로 다시 시도합니다.
 *
 * <p>금칙어 검사는 하지 않습니다. 단어를 사람이 골라 넣으므로 조합이 금칙어를 만들 수 없다는 것을
 * {@code NicknameGeneratorTest} 가 전수로 확인합니다.
 */
@Component
public class NicknameGenerator {

    static final List<String> ADJECTIVES = List.of(
            "Bold", "Calm", "Cool", "Fast", "Kind", "Wild", "Warm", "Soft",
            "Tiny", "Wise", "Keen", "Neat", "Glad", "Deft", "Zany", "Spry"
    );

    /** 클라이언트 Pseudonym.cs 의 Nouns 와 같은 순서, 같은 단어입니다. */
    static final List<String> NOUNS = List.of(
            "Fox", "Owl", "Bear", "Wolf", "Deer", "Seal", "Lynx", "Puma",
            "Duck", "Crow", "Hawk", "Mole", "Toad", "Frog", "Hare", "Moth"
    );

    public String generate() {
        ThreadLocalRandom random = ThreadLocalRandom.current();
        String adjective = ADJECTIVES.get(random.nextInt(ADJECTIVES.size()));
        String noun = NOUNS.get(random.nextInt(NOUNS.size()));
        int number = random.nextInt(1000, 10000);
        return adjective + noun + number;
    }
}
