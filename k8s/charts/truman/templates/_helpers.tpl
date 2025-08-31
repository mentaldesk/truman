{{- define "truman.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" -}}
{{- end -}}

{{- define "truman.fullname" -}}
{{- if .Values.fullnameOverride -}}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- $name := default .Chart.Name .Values.nameOverride -}}
{{- if contains $name .Release.Name -}}
{{- .Release.Name | trunc 63 | trimSuffix "-" -}}
{{- else -}}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" -}}
{{- end -}}
{{- end -}}
{{- end -}}

{{- define "truman.labels" -}}
app.kubernetes.io/name: {{ include "truman.name" . }}
helm.sh/chart: {{ .Chart.Name }}-{{ .Chart.Version | replace "+" "_" }}
app.kubernetes.io/instance: {{ .Release.Name }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- with .Values.podLabels }}
{{ toYaml . | nindent 0 }}
{{- end }}
{{- end -}}

{{- define "truman.selectorLabels" -}}
app.kubernetes.io/name: {{ include "truman.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- with .Values.selectorLabels }}
{{ toYaml . | nindent 0 }}
{{- end }}
{{- end -}}

{{- define "truman.configmap.name" -}}
{{ printf "%s-config" (include "truman.fullname" .) }}
{{- end -}}

{{- define "truman.secret.name" -}}
{{ .Values.secret.existingName | default (printf "%s-env" (include "truman.fullname" .)) }}
{{- end -}}

