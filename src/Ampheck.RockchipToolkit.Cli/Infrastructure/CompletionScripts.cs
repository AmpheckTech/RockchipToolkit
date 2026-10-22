namespace Ampheck.RockchipToolkit.Cli.Infrastructure;

internal static class CompletionScripts {
    public static string Get(string shell) {
        return shell.ToLowerInvariant() switch {
            "bash" => Bash,
            "zsh" => Zsh,
            "powershell" => PowerShell,
            "fish" => Fish,
            _ => throw new ArgumentException(
                $"Unsupported completion shell '{shell}'.")
        };
    }

    private const string Bash = """
        _arkt_complete()
        {
            local exe="${COMP_WORDS[0]}"
            local line="${COMP_LINE:0:COMP_POINT}"
            local args="${line#* }"
            [[ "$line" == *" "* ]] || args=""
            local suggestions
            mapfile -t suggestions < <(
                "$exe" "[suggest:${#args}]" "$args"
            )
            local IFS=$'\n'
            COMPREPLY=($(compgen -W "$(printf '%s\n' "${suggestions[@]}")" \
                -- "${COMP_WORDS[COMP_CWORD]}"))
        }
        complete -F _arkt_complete arkt
        """;

    private const string Zsh = """
        _arkt_complete()
        {
            local exe="${words[1]}"
            local args="${words[2,-1]}"
            local suggestions
            suggestions=("${(@f)$(
                "$exe" "[suggest:${#args}]" "$args"
            )}")
            compadd -- "${suggestions[@]}"
        }
        compdef _arkt_complete arkt
        """;

    private const string PowerShell = """
        Register-ArgumentCompleter -Native -CommandName arkt -ScriptBlock {
            param($wordToComplete, $commandAst, $cursorPosition)

            $exe = (Get-Command $commandAst.CommandElements[0]).Source
            $line = $commandAst.Extent.Text
            $separator = $line.IndexOf(' ')
            if ($separator -lt 0) {
                $arguments = ''
            }
            else {
                $arguments = $line.Substring($separator + 1)
            }

            & $exe "[suggest:$($arguments.Length)]" $arguments |
                ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new(
                        $_,
                        $_,
                        'ParameterValue',
                        $_)
                }
        }
        """;

    private const string Fish = """
        function __arkt_complete
            set -l line (commandline -cp)
            set -l parts (string split -m1 ' ' -- $line)
            if test (count $parts) -lt 2
                set -l suggestions ($parts[1] "[suggest:0]" "")
            else
                set -l args $parts[2]
                set -l suggestions ($parts[1] "[suggest:"(string length -- $args)"]" $args)
            end
            printf '%s\n' $suggestions
        end

        complete -c arkt -f -a '(__arkt_complete)'
        """;
}
