package com.ssafy.d205.domain.highlight;

import jakarta.validation.Valid;
import jakarta.validation.constraints.*;
import java.util.List;
import lombok.RequiredArgsConstructor;
import org.springframework.web.bind.annotation.*;
import org.springframework.web.server.ResponseStatusException;
import org.springframework.http.HttpStatus;
import com.ssafy.d205.global.security.AccountTokens;

@RestController
@RequestMapping("/api/v1/highlights/director")
@RequiredArgsConstructor
public class HighlightDirectorController {
    private final HighlightDirectorService service;
    private final AccountTokens tokens;
    public record Candidate(@Min(0) @Max(9) int id,
            @NotNull @Pattern(regexp="FirstBlood|TteTanMulgun|FinalMoment|LongestHidden|MostStunned|ItemDestroyed|PlayerStunned|ItemRecovered") String eventType,
            @DecimalMin("0.01") @DecimalMax("120") double seconds,
            @Min(1) @Max(8) int segments,
            @DecimalMin("0") @DecimalMax("86400") double remainingSeconds,
            @Min(0) @Max(6) int involvedPlayers,
            @DecimalMin("0") @DecimalMax("1000") double ruleScore) {}
    public record DirectorRequest(@NotNull @Size(min=1,max=10) List<@NotNull @Valid Candidate> candidates) {}
    @PostMapping
    public HighlightDirectorService.DirectorReply direct(@RequestHeader("X-User-Id") String user,
            @RequestHeader(value="X-Account-Token",defaultValue="") String token,
            @Valid @RequestBody DirectorRequest request) {
        if (!service.enabled() || !tokens.isEnabled()) return HighlightDirectorService.DirectorReply.unavailable();
        if (!tokens.matches(user,token)) throw new ResponseStatusException(HttpStatus.UNAUTHORIZED);
        if (request.candidates().stream().map(Candidate::id).distinct().count()!=request.candidates().size())
            throw new ResponseStatusException(HttpStatus.BAD_REQUEST);
        return service.direct(request.candidates());
    }
}
