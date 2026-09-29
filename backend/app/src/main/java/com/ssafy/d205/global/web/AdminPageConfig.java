package com.ssafy.d205.global.web;

import org.springframework.context.annotation.Configuration;
import org.springframework.web.servlet.config.annotation.ViewControllerRegistry;
import org.springframework.web.servlet.config.annotation.WebMvcConfigurer;

/**
 * /admin/ 을 관리 화면으로 이어 줍니다.
 *
 * <p>정적 파일은 그냥 두면 <b>/admin/index.html 로만 열립니다.</b> 스프링이 자동으로
 * 이어주는 시작 문서는 최상위 하나뿐이고, 하위 디렉터리를 가리키는 요청은 파일이
 * 아니라서 404 가 됩니다.
 *
 * <p>운영자에게 "주소 끝에 index.html 을 붙이세요"라고 안내하는 것은 답이 아닙니다.
 * 링크를 주고받거나 북마크할 때마다 틀립니다.
 *
 * <p>/admin 도 함께 넘깁니다. 뒤 슬래시가 없는 주소를 치는 쪽이 더 흔합니다.
 */
@Configuration
public class AdminPageConfig implements WebMvcConfigurer {

    @Override
    public void addViewControllers(ViewControllerRegistry registry) {
        registry.addViewController("/admin").setViewName("forward:/admin/index.html");
        registry.addViewController("/admin/").setViewName("forward:/admin/index.html");
    }
}
